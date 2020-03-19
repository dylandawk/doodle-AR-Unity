using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UtilsModule;
using OpenCVForUnityExample;

public class Scanner : MonoBehaviour
{
    Texture2D baseTexture;
    [SerializeField]
    RawImage sourceRawImage;
    [SerializeField]
    RawImage targetRawImage;

    public WebCamPerspectiveWarp warp;
    public WWWFormImage www;

    private Texture2D finalTexture;


    // Start is called before the first frame update
    void Start()
    {
        if (warp.hasInitDone)
        {
            baseTexture = warp.texture;
            FindLines();
        }
        
    }

    bool hasFoundLines;
    // Update is called once per frame
    void Update()
    {
        if (warp.hasInitDone)
        {
            baseTexture = warp.texture;
            sourceRawImage.texture = baseTexture;
            FormatImageSquare();
            //FindLines();
        }
    }

    public void UploadTexture()
    {
        if(finalTexture != null)
        {
            www.UploadImage(finalTexture);
        }
    }

    //finds lines in image
    void FindLines()
    {
        Mat mainMat = new Mat(baseTexture.height, baseTexture.width, CvType.CV_8UC3);
        Mat grayMat = new Mat();

        //Convert Texture2d to Matrix
        Utils.texture2DToMat(baseTexture, mainMat);
        //copy main matrix to grayMat
        mainMat.copyTo(grayMat);

        //Convert color to gray
        Imgproc.cvtColor(grayMat, grayMat, Imgproc.COLOR_BGR2GRAY);

        //Convert to canny edges
        Mat edges = new Mat();
        Imgproc.Canny(grayMat, edges, 50, 200);

        //convert to lines
        Mat lines = new Mat(edges.rows(), edges.cols(), CvType.CV_32SC4);
        int minLineLength = 50;
        int maxLineGap = 150;
        Imgproc.HoughLinesP(edges, lines, 1, Mathf.PI / 180, 75, minLineLength, maxLineGap);
        Debug.Log("lines.toString() " + lines.ToString());
        Debug.Log ("lines.dump()" + lines.dump ());

        int[] linesArray = new int[lines.cols() * lines.rows() * lines.channels()];
        lines.get(0, 0, linesArray);

        for (int i = 0; i < linesArray.Length; i = i + 4) 
        {
            Imgproc.line(grayMat, new Point(linesArray[i + 0], linesArray[ i + 1]), new Point(linesArray[i + 2], linesArray[i + 3]), new Scalar(255, 0, 0), 2);
        }



        Texture2D finalTexture = new Texture2D(grayMat.cols(), grayMat.rows(), TextureFormat.RGB24, false);
        Utils.matToTexture2D(grayMat, finalTexture);
        targetRawImage.texture = finalTexture;
    }

    void FormatImageSquare()
    {
        Mat mainMat = new Mat(baseTexture.height, baseTexture.width, CvType.CV_8UC3);
        Mat grayMat = new Mat();

        //Convert Texture2d to Matrix
        Utils.texture2DToMat(baseTexture, mainMat);
        //copy main matrix to grayMat
        mainMat.copyTo(grayMat);

        //Convert color to gray
        Imgproc.cvtColor(grayMat, grayMat, Imgproc.COLOR_BGR2GRAY);

        //Blur
        Imgproc.GaussianBlur(grayMat, grayMat, new Size(25, 25), 0);

        //contrast
        Imgproc.threshold(grayMat, grayMat, 0, 255, Imgproc.THRESH_OTSU);
        //extract edge
        Imgproc.Canny(grayMat, grayMat, 50, 50);

        //prepare for the finding contours
        List<MatOfPoint> contours = new List<MatOfPoint>();
        //find the contour from canny edge image
        Imgproc.findContours(grayMat, contours, new Mat(), Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

        List<MatOfPoint> tempTargets = new List<MatOfPoint>();

        for (int i = 0; i < contours.Count; i++)
        {
            MatOfPoint cp = contours[i];
            MatOfPoint2f cn = new MatOfPoint2f(cp.toArray());
            double p = Imgproc.arcLength(cn, true);

            MatOfPoint2f approx = new MatOfPoint2f();
            //convret to polygon
            Imgproc.approxPolyDP(cn, approx, 0.03 * p, true);


            //find a contour with four points and large area
            int minContourArea = 10000;
            if (approx.toArray().Length == 4 && Imgproc.contourArea(approx) > minContourArea)
            {
                MatOfPoint approxPt = new MatOfPoint();
                approx.convertTo(approxPt, CvType.CV_32S);

                float maxCosine = 0;
                for (int j = 2; j < 5; j++)
                {
                    Vector2 v1 = new Vector2((float)(approx.toArray()[j % 4].x - approx.toArray()[j - 1].x), (float)(approx.toArray()[j % 4].y - approx.toArray()[j - 1].y));
                    Vector2 v2 = new Vector2((float)(approx.toArray()[j - 2].x - approx.toArray()[j - 1].x), (float)(approx.toArray()[j - 2].y - approx.toArray()[j - 1].y));

                    float angle = Mathf.Abs(Vector2.Angle(v1, v2));
                    maxCosine = Mathf.Max(maxCosine, angle);

                }

                if (maxCosine < 135f)
                {
                    tempTargets.Add(approxPt);
                }
            }
        }

        if (tempTargets.Count > 0)
        {
            //Get the first contour 
            MatOfPoint approxPt = tempTargets[0];
            //Making Source Mat
            Mat srcPointMat = Converters.vector_Point_to_Mat(approxPt.toList(), CvType.CV_32F);

            //Making Destination Mat
            /*change these values*/
            List<Point> dstPoints = new List<Point>();
            dstPoints.Add(new Point(512, 0));
            dstPoints.Add(new Point(0, 0));
            dstPoints.Add(new Point(0, 512));
            dstPoints.Add(new Point(512, 512));

            Mat dstPointMat = Converters.vector_Point_to_Mat(dstPoints, CvType.CV_32F);

            //Make Perp transform
            Mat M = Imgproc.getPerspectiveTransform(srcPointMat, dstPointMat);
            Mat warpedMat = new Mat(mainMat.size(), mainMat.type());
            //Crop and warp the image
            Imgproc.warpPerspective(mainMat, warpedMat, M, new Size(512, 512), Imgproc.INTER_LINEAR);
            warpedMat.convertTo(warpedMat, CvType.CV_8UC3);

            
            

            //Convert color to gray
            Imgproc.cvtColor(warpedMat, warpedMat, Imgproc.COLOR_BGR2GRAY);
            ////Blur
            //Imgproc.GaussianBlur(warpedMat, warpedMat, new Size(25, 25), 0);

            //contrast
            Imgproc.threshold(warpedMat, warpedMat, 0, 255, Imgproc.THRESH_OTSU);
            //resize
            Imgproc.resize(warpedMat, warpedMat, new Size(28, 28));
            //Create an empty final texture
            finalTexture = new Texture2D(warpedMat.width(), warpedMat.height(), TextureFormat.RGB24, false);
            //Convert material to texture2d
            Utils.matToTexture2D(warpedMat, finalTexture);
            targetRawImage.texture = finalTexture;
        }
    }
}
