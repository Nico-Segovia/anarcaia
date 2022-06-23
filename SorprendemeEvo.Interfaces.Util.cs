using NLog;
using SorprendemeEvo.Database;
using SorprendemeEvo.Database.constants;
using SorprendemeEvo.Database.general;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace SorprendemeEvo.Interfaces.Util
{
    public class Imagen
    {
        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern int DeleteDC(IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern int BitBlt(IntPtr hdcDst, int xDst, int yDst, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, int rop);
        static int SRCCOPY = 0x00CC0020;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint Usage, out IntPtr bits, IntPtr hSection, uint dwOffset);
        static uint BI_RGB = 0;
        static uint DIB_RGB_COLORS = 0;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public uint biSize;
            public int biWidth, biHeight;
            public short biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 256)]
            public uint[] cols;
        }

        static uint MAKERGB(int r, int g, int b) => ((uint)(b & 255)) | ((uint)((r & 255) << 8)) | ((uint)((g & 255) << 16));

        public static void GetEncoderParameters(out EncoderParameters eps, out ImageCodecInfo tiffEncoder, Bitmap bmp)
        {
            eps = new EncoderParameters();

            switch (bmp.PixelFormat)
            {
                case PixelFormat.Format1bppIndexed:
                    eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, (long)EncoderValue.CompressionCCITT4);
                    break;
                default:
                    eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, (long)EncoderValue.CompressionLZW);
                    break;
            }

            ImageCodecInfo[] ie = ImageCodecInfo.GetImageEncoders();
            tiffEncoder = null;

            for (int j = 0; j < ie.Length; j++)
            {
                if (ie[j].MimeType == "image/tiff")
                {
                    tiffEncoder = ie[j];
                    break;
                }
            }
        }

        public static void GetEncoderJPG(out ImageCodecInfo jpgEncoder)
        {
            jpgEncoder = null;

            ImageCodecInfo[] ie = ImageCodecInfo.GetImageEncoders();

            for (int j = 0; j < ie.Length; j++)
            {
                if (ie[j].MimeType == "image/jpeg")
                {
                    jpgEncoder = ie[j];
                    break;
                }
            }
        }

        public static Bitmap CargarBitmap(string archivo)
        {
            FileStream fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            BinaryReader br = new BinaryReader(fs);
            MemoryStream ms = new MemoryStream(br.ReadBytes((int)fs.Length));

            Bitmap bmp = new Bitmap(ms);

            ms.Close();
            br.Close();
            fs.Close();
            ms.Dispose();
            br.Dispose();
            fs.Dispose();

            return bmp;
        }

        public static Bitmap CargarBitmapConverter(string archivo)
        {
            Bitmap bmp = (Bitmap)Image.FromFile(archivo);

            //FileStream fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            //BinaryReader br = new BinaryReader(fs);
            //MemoryStream ms = new MemoryStream(br.ReadBytes((int)fs.Length));

            //Bitmap bmp = new Bitmap(ms);

            bmp = EscalarBitmap(bmp);

            int maxTamImagen = 0;
            if (SysConf.GetSysConf(SysConf_Constants.EXPORTACION_MAXTAMIMAGEN) != null)
                maxTamImagen = int.Parse(SysConf.GetSysConf(SysConf_Constants.EXPORTACION_MAXTAMIMAGEN));

            if (maxTamImagen > 0)
            {
                FileInfo fi = new FileInfo(archivo);
                int maxLength = maxTamImagen;
                if (fi.Length > maxLength)
                {
                    double coef = (double)fi.Length / maxLength;
                    if (bmp.Width > bmp.Height)
                        bmp = ResizeBitmap(bmp, (int)(bmp.Width / coef), (int)(bmp.Width / coef));
                    else
                        bmp = ResizeBitmap(bmp, (int)(bmp.Height / coef), (int)(bmp.Height / coef));

                    bmp = EscalarBitmap(bmp);
                }
            }

            return bmp;
        }

        private static Bitmap EscalarBitmap(Bitmap bmp)
        {
            int resolution = 96;
            float realWidth = bmp.Width / bmp.HorizontalResolution * 72;
            float realHeight = bmp.Height / bmp.VerticalResolution * 72;

            //Si alguno es mayor a 14400 lo reduce (límite de itextsharp)
            float escala = 1;
            if (realWidth > 14400 && realHeight > 14400)
            {
                if (realWidth <= realHeight)
                {
                    escala = (float)(14200 * resolution / 72) / bmp.Width;
                }
                else
                {
                    escala = (float)(14200 * resolution / 72) / bmp.Height;
                }
            }

            if (realWidth > 14400 && realHeight < 14400)
            {
                escala = (float)(14200 * resolution / 72) / bmp.Width;
            }

            if (realWidth < 14400 && realHeight > 14400)
            {
                escala = (float)(14200 * resolution / 72) / bmp.Height;
            }

            if (escala != 1)
            {
                bmp = new Bitmap(bmp, new Size((int)(bmp.Width * escala), (int)(bmp.Height * escala)));
            }

            return bmp;
        }

        public static Bitmap CargarBitmapVisor(string archivo)
        {
            FileStream fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            BinaryReader br = new BinaryReader(fs);
            MemoryStream ms = new MemoryStream(br.ReadBytes((int)fs.Length));

            Bitmap bmp = new Bitmap(ms);

            ms.Close();
            br.Close();
            fs.Close();
            ms.Dispose();
            br.Dispose();
            fs.Dispose();

            int maxTamImagen = 0;
            if (SysConf.GetSysConf(SysConf_Constants.EXPORTACION_MAXTAMIMAGEN) != null)
                maxTamImagen = int.Parse(SysConf.GetSysConf(SysConf_Constants.EXPORTACION_MAXTAMIMAGEN));

            if (maxTamImagen > 0)
            {
                FileInfo fi = new FileInfo(archivo);
                int maxLength = maxTamImagen;
                if (fi.Length > maxLength)
                {
                    double coef = (double)fi.Length / maxLength;
                    if (bmp.Width > bmp.Height)
                        bmp = ResizeBitmap(bmp, (int)(bmp.Width / coef), (int)(bmp.Width / coef));
                    else
                        bmp = ResizeBitmap(bmp, (int)(bmp.Height / coef), (int)(bmp.Height / coef));
                }
            }

            return bmp;
        }

        /// <summary>
        /// Crea la imagen (Bitmap) a partir de una ruta de archivo. <br></br>
        /// <exception cref="Exception">Si la imagen esta rota: loguea el error y devuelve null</exception>
        /// <br><b>El manejo correcto de errores depende de quien implemente esta funcion.</b></br>
        /// </summary>
        /// <param name="archivo">Nombre del archivo. En base de datos es Documento.ArchivoOriginal</param>
        /// <returns>Una imagen (Bitmap)</returns>
        public static Bitmap CargarBitmapVisor2(string archivo)
        {
            Bitmap bmp;
            try
            {
                bmp = (Bitmap)Image.FromFile(archivo);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error cargando la imagen {archivo} | Excepcion: {ex.GetType().FullName} - {ex.Message}");
                return null;
            }

            int maxTamImagen = 0;
            if (SysConf.GetSysConf(SysConf_Constants.EXPORTACION_MAXTAMIMAGEN) != null)
                maxTamImagen = int.Parse(SysConf.GetSysConf(SysConf_Constants.EXPORTACION_MAXTAMIMAGEN));

            if (maxTamImagen > 0)
            {
                FileInfo fi = new FileInfo(archivo);
                int maxLength = maxTamImagen;
                if (fi.Length > maxLength)
                {
                    double coef = (double)fi.Length / maxLength;
                    if (bmp.Width > bmp.Height)
                        bmp = ResizeBitmap(bmp, (int)(bmp.Width / coef), (int)(bmp.Width / coef));
                    else
                        bmp = ResizeBitmap(bmp, (int)(bmp.Height / coef), (int)(bmp.Height / coef));
                }
            }
            return bmp;
        }

        public static Bitmap CargarBitmap(byte[] blob)
        {
            MemoryStream mStream = new MemoryStream();
            byte[] pData = blob;
            mStream.Write(pData, 0, Convert.ToInt32(pData.Length));
            Bitmap bm = new Bitmap(mStream, false);
            mStream.Dispose();
            return bm;
        }

        public static Bitmap LeerBitmap(string archivo, int cantIntentos)
        {
            Bitmap bmp = null;
            int cont = 0;

            while ((cont < cantIntentos) && (bmp == null))
            {
                try
                {
                    if (!IsFileLocked(archivo))
                        bmp = Imagen.CargarBitmap(archivo);
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                    cont++;
                }
            }

            return bmp;
        }

        public static bool IsFileLocked(string fileName)
        {
            FileStream stream = null;

            try
            {
                stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        public static Bitmap ResizeBitmap(Bitmap b, int nWidth, int nHeight)
        {
            try
            {
                float scaleHeight = nHeight / (float)b.Height;
                float scaleWidth = nWidth / (float)b.Width;

                float scale = Math.Min(scaleHeight, scaleWidth);

                Image result = b.GetThumbnailImage((int)(b.Width * scale), (int)(b.Height * scale), null, IntPtr.Zero);

                return (Bitmap)result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
            finally
            {
                b.Dispose();
            }
        }

        public static Bitmap ResizeBitmap2(Bitmap b, int nWidth, int nHeight)
        {
            Bitmap result = new Bitmap(nWidth, nHeight);
            Graphics g = Graphics.FromImage(result);
            g.Clear(Color.Gainsboro);

            Image bthumb = b.GetThumbnailImage(nWidth, nHeight, null, IntPtr.Zero);

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            if (b.Width > b.Height)
            {
                double nheight = b.Height * (nWidth / (double)b.Width);
                double ntop = (nHeight - nheight) / 2;
                g.DrawImage(bthumb, 0, (int)ntop, nWidth, (int)nheight);
            }
            else
            {
                double nwidth = b.Width * (nHeight / (double)b.Height);
                double nleft = (nWidth - nwidth) / 2;
                g.DrawImage(bthumb, (int)nleft, 0, (int)nwidth, nHeight);
            }

            g.Dispose();

            return result;
        }

        /// <summary>
        /// Copies a bitmap into a 1bpp/8bpp bitmap of the same dimensions, fast
        /// </summary>
        /// <param name="b">original bitmap</param>
        /// <param name="bpp">1 or 8, target bpp</param>
        /// <returns>a 1bpp copy of the bitmap</returns>
        public static System.Drawing.Bitmap CopyToBpp(System.Drawing.Bitmap b, int bpp)
        {
            if (bpp != 1 && bpp != 8) throw new System.ArgumentException("1 or 8", "bpp");

            // Plan: built into Windows GDI is the ability to convert
            // bitmaps from one format to another. Most of the time, this
            // job is actually done by the graphics hardware accelerator card
            // and so is extremely fast. The rest of the time, the job is done by
            // very fast native code.
            // We will call into this GDI functionality from C#. Our plan:
            // (1) Convert our Bitmap into a GDI hbitmap (ie. copy unmanaged->managed)
            // (2) Create a GDI monochrome hbitmap
            // (3) Use GDI "BitBlt" function to copy from hbitmap into monochrome (as above)
            // (4) Convert the monochrone hbitmap into a Bitmap (ie. copy unmanaged->managed)

            int w = b.Width, h = b.Height;
            IntPtr hbm = b.GetHbitmap(); // this is step (1)
            int allocatedSizeHbm = w * h * 4; // each pixel takes ~4 bytes?!
            GC.AddMemoryPressure(allocatedSizeHbm);
            //
            // Step (2): create the monochrome bitmap.
            // "BITMAPINFO" is an interop-struct which we define below.
            // In GDI terms, it's a BITMAPHEADERINFO followed by an array of two RGBQUADs
            BITMAPINFO bmi = new BITMAPINFO
            {
                biSize = 40,  // the size of the BITMAPHEADERINFO struct
                biWidth = w,
                biHeight = h,
                biPlanes = 1, // "planes" are confusing. We always use just 1. Read MSDN for more info.
                biBitCount = (short)bpp, // ie. 1bpp or 8bpp
                biCompression = BI_RGB, // ie. the pixels in our RGBQUAD table are stored as RGBs, not palette indexes
                biSizeImage = (uint)(((w + 7) & 0xFFFFFFF8) * h / 8),
                biXPelsPerMeter = 1000000, // not really important
                biYPelsPerMeter = 1000000 // not really important
            };
            // Now for the colour table.
            uint ncols = (uint)1 << bpp; // 2 colours for 1bpp; 256 colours for 8bpp
            bmi.biClrUsed = ncols;
            bmi.biClrImportant = ncols;
            bmi.cols = new uint[256]; // The structure always has fixed size 256, even if we end up using fewer colours
            if (bpp == 1) { bmi.cols[0] = MAKERGB(0, 0, 0); bmi.cols[1] = MAKERGB(255, 255, 255); }
            else { for (int i = 0; i < ncols; i++) bmi.cols[i] = MAKERGB(i, i, i); }
            // For 8bpp we've created an palette with just greyscale colours.
            // You can set up any palette you want here. Here are some possibilities:
            // greyscale: for (int i=0; i<256; i++) bmi.cols[i]=MAKERGB(i,i,i);
            // rainbow: bmi.biClrUsed=216; bmi.biClrImportant=216; int[] colv=new int[6]{0,51,102,153,204,255};
            //          for (int i=0; i<216; i++) bmi.cols[i]=MAKERGB(colv[i/36],colv[(i/6)%6],colv[i%6]);
            // optimal: a difficult topic: http://en.wikipedia.org/wiki/Color_quantization
            // 
            // Now create the indexed bitmap "hbm0"
            IntPtr bits0; // not used for our purposes. It returns a pointer to the raw bits that make up the bitmap.
            IntPtr hbm0 = CreateDIBSection(IntPtr.Zero, ref bmi, DIB_RGB_COLORS, out bits0, IntPtr.Zero, 0);
            //
            // Step (3): use GDI's BitBlt function to copy from original hbitmap into monocrhome bitmap
            // GDI programming is kind of confusing... nb. The GDI equivalent of "Graphics" is called a "DC".
            IntPtr sdc = GetDC(IntPtr.Zero);       // First we obtain the DC for the screen
            // Next, create a DC for the original hbitmap
            IntPtr hdc = CreateCompatibleDC(sdc); SelectObject(hdc, hbm);
            // and create a DC for the monochrome hbitmap
            IntPtr hdc0 = CreateCompatibleDC(sdc); SelectObject(hdc0, hbm0);
            // Now we can do the BitBlt:
            BitBlt(hdc0, 0, 0, w, h, hdc, 0, 0, SRCCOPY);
            // Step (4): convert this monochrome hbitmap back into a Bitmap:
            System.Drawing.Bitmap b0 = System.Drawing.Bitmap.FromHbitmap(hbm0);
            //
            // Finally some cleanup.
            DeleteDC(hdc);
            DeleteDC(hdc0);
            ReleaseDC(IntPtr.Zero, sdc);
            DeleteObject(hbm);
            DeleteObject(hbm0);
            GC.RemoveMemoryPressure(allocatedSizeHbm);
            //
            return b0;
        }
    }
}
