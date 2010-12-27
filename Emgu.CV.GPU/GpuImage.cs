﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;

namespace Emgu.CV.GPU
{
   /// <summary>
   /// An GpuImage is very similar to the Emgu.CV.Image except that it is being used for GPU processing
   /// </summary>
   /// <typeparam name="TColor">Color type of this image (either Gray, Bgr, Bgra, Hsv, Hls, Lab, Luv, Xyz or Ycc)</typeparam>
   /// <typeparam name="TDepth">Depth of this image (either Byte, SByte, Single, double, UInt16, Int16 or Int32)</typeparam>
   public class GpuImage<TColor, TDepth>
      : GpuMat<TDepth>, IImage
      where TColor : struct, IColor
      where TDepth : new()
   {
      /// <summary>
      /// Create a GPU image from a regular image
      /// </summary>
      /// <param name="img">The image to be converted to GPU image</param>
      public GpuImage(Image<TColor, TDepth> img)
         : base(img)
      {
      }

      /// <summary>
      /// Create a GpuImage of the specific size
      /// </summary>
      /// <param name="rows">The number of rows (height)</param>
      /// <param name="cols">The number of columns (width)</param>
      public GpuImage(int rows, int cols)
         : base(rows, cols, new TColor().Dimension)
      {
      }

      /// <summary>
      /// Create a GpuImage of the specific size
      /// </summary>
      /// <param name="size">The size of the image</param>
      public GpuImage(Size size)
         : this(size.Height, size.Width)
      {
      }

      /// <summary>
      /// Convert the current GpuImage to a regular Image.
      /// </summary>
      /// <returns>A regular image</returns>
      public Image<TColor, TDepth> ToImage()
      {
         Image<TColor, TDepth> img = new Image<TColor, TDepth>(Size);
         Download(img);
         return img;
      }

      /*
      public Image<TOtherColor, TOtherDepth> Convert<TOtherColor, TOtherDepth>()
         where TOtherColor : struct, IColor
         where TOtherDepth : new()
      {
         Image<TOtherColor, TOtherDepth> res = new Image<TOtherColor, TOtherDepth>(Size);
         res.ConvertFrom(this);
         return res;
      }

      /// <summary>
      /// Convert the source image to the current image, if the size are different, the current image will be a resized version of the srcImage. 
      /// </summary>
      /// <typeparam name="TSrcColor">The color type of the source image</typeparam>
      /// <typeparam name="TSrcDepth">The color depth of the source image</typeparam>
      /// <param name="srcImage">The sourceImage</param>
      public void ConvertFrom<TSrcColor, TSrcDepth>(GpuImage<TSrcColor, TSrcDepth> srcImage)
         where TSrcColor : struct, IColor
         where TSrcDepth : new()
      {
         if (!Size.Equals(srcImage.Size))
         {  //if the size of the source image do not match the size of the current image
            using (GpuImage<TSrcColor, TSrcDepth> tmp = srcImage.Resize(Width, Height, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR))
            {
               ConvertFrom(tmp);
               return;
            }
         }

         if (typeof(TColor) == typeof(TSrcColor))
         {
            #region same color
            if (typeof(TDepth) == typeof(TSrcDepth))
            {   //same depth
               CvInvoke.cvCopy(srcImage.Ptr, Ptr, IntPtr.Zero);
            }
            else
            {
               //different depth
               //int channelCount = NumberOfChannels;
               {
                  if (typeof(TDepth) == typeof(Byte) && typeof(TSrcDepth) != typeof(Byte))
                  {
                     double[] minVal, maxVal;
                     Point[] minLoc, maxLoc;
                     srcImage.MinMax(out minVal, out maxVal, out minLoc, out maxLoc);
                     double min = minVal[0];
                     double max = maxVal[0];
                     for (int i = 1; i < minVal.Length; i++)
                     {
                        min = Math.Min(min, minVal[i]);
                        max = Math.Max(max, maxVal[i]);
                     }
                     double scale = 1.0, shift = 0.0;
                     if (max > 255.0 || min < 0)
                     {
                        scale = (max == min) ? 0.0 : 255.0 / (max - min);
                        shift = (scale == 0) ? min : -min * scale;
                     }

                     CvInvoke.cvConvertScaleAbs(srcImage.Ptr, Ptr, scale, shift);
                  }
                  else
                  {
                     CvInvoke.cvConvertScale(srcImage.Ptr, Ptr, 1.0, 0.0);
                  }
               }
            }
            #endregion
         }
         else
         {
            #region different color
            if (typeof(TDepth) == typeof(TSrcDepth))
            {   //same depth
               ConvertColor(srcImage.Ptr, Ptr, typeof(TSrcColor), typeof(TColor), Size);
            }
            else
            {   //different depth
               using (Image<TSrcColor, TDepth> tmp = srcImage.Convert<TSrcColor, TDepth>()) //convert depth
                  ConvertColor(tmp.Ptr, Ptr, typeof(TSrcColor), typeof(TColor), Size);
            }
            #endregion
         }
      }*/

      private static void ConvertColor(IntPtr src, IntPtr dest, Type srcColor, Type destColor, Size size)
      {
         try
         {
            // if the direct conversion exist, apply the conversion
            GpuInvoke.gpuMatCvtColor(src, dest, Util.GetColorCvtCode(srcColor, destColor));
         }
         catch
         {
            try
            {
               //if a direct conversion doesn't exist, apply a two step conversion
               using (GpuImage<Bgr, TDepth> tmp = new GpuImage<Bgr, TDepth>(size))
               {
                  GpuInvoke.gpuMatCvtColor(src, tmp.Ptr, Util.GetColorCvtCode(srcColor, typeof(Bgr)));
                  GpuInvoke.gpuMatCvtColor(tmp.Ptr, dest, Util.GetColorCvtCode(typeof(Bgr), destColor));
               }
            }
            catch
            {
               throw new NotSupportedException(String.Format(
                  "Convertion from Image<{0}, {1}> to Image<{2}, {3}> is not supported by OpenCV",
                  srcColor.ToString(),
                  typeof(TDepth).ToString(),
                  destColor.ToString(),
                  typeof(TDepth).ToString()));
            }
         }
      }

      /// <summary>
      /// Create a clone of this GpuImage
      /// </summary>
      /// <returns>A clone of this GpuImage</returns>
      public GpuImage<TColor, TDepth> Clone()
      {
         GpuImage<TColor, TDepth> result = new GpuImage<TColor, TDepth>(Size);
         GpuInvoke.gpuMatCopy(_ptr, result, IntPtr.Zero);
         return result;
      }

      #region IImage Members
      /// <summary>
      /// convert the current GpuImage to its equavalent Bitmap representation
      /// </summary>
      public Bitmap Bitmap
      {
         get 
         {
            using (Image<TColor, TDepth> tmp = ToImage())
            {
               return tmp.ToBitmap();
            }
         }
      }

      public void MinMax(out double[] minValues, out double[] maxValues, out Point[] minLocations, out Point[] maxLocations)
      {
         throw new NotImplementedException();
      }

      public IImage[] Split()
      {
         throw new NotImplementedException();
      }

      /// <summary>
      /// Saving the GPU image to file
      /// </summary>
      /// <param name="fileName">The file to be saved to</param>
      public void Save(string fileName)
      {
         using (Image<TColor, TDepth> tmp = ToImage())
         {
            tmp.Save(fileName);
         }
      }

      #endregion

      #region ICloneable Members

      object ICloneable.Clone()
      {
         return Clone();
      }

      #endregion
   }
}
