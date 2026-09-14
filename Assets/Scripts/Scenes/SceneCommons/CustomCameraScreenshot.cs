using System;
using System.IO;
using UnityEngine;

public static class CustomCameraScreenshot
{
    public static void TakeScreenshot(string folder = "", string fileName = "", Camera camera = null, int width = 1920, int height = 1080, int scale = 1, bool isTransparent = false)
    {
        if (camera == null)
        {
            camera = Camera.main;
        }

        if (camera == null)
        {
            Debug.LogError("No Camera found. Please assign a camera first.");
            return;
        }

        if (folder == "")
        {
            folder = Application.dataPath;
        }

        int num = width * scale;
        int num2 = height * scale;
        RenderTexture active = (camera.targetTexture = new RenderTexture(num, num2, 24));
        TextureFormat textureFormat = ((!isTransparent) ? TextureFormat.RGB24 : TextureFormat.ARGB32);
        Texture2D texture2D = new Texture2D(num, num2, textureFormat, mipChain: false);
        camera.Render();
        try
        {
            RenderTexture.active = active;
            texture2D.ReadPixels(new Rect(0f, 0f, num, num2), 0, 0);
            byte[] bytes = texture2D.EncodeToPNG();
            string text = "";
            if (fileName == "")
            {
                text = ScreenShotName(num, num2, folder);
            }
            else
            {
                if (folder[folder.Length - 1] != '/')
                {
                    folder += "/";
                }

                text = folder + fileName;
            }

            File.WriteAllBytes(text, bytes);
            Debug.Log("Screenshot Saved at : " + text);
            camera.targetTexture = null;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.ToString());
        }

        RenderTexture.active = null;
        camera.targetTexture = null;
    }

    public static string ScreenShotName(int width, int height, string strPath = "")
    {
        if (strPath == "")
        {
            strPath = Application.dataPath;
        }

        if (strPath[strPath.Length - 1] != '/')
        {
            strPath += "/";
        }

        string text = string.Format("screenshot_{0}x{1}_{2}.png", width, height, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        strPath += text;
        return strPath;
    }
}