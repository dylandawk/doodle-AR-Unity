using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class WWWFormImage : MonoBehaviour
{

    public string imageURL = "localhost:3000/api/image";

    // Use this for initialization
    void Start()
    {
        //StartCoroutine(Upload());
    }

    IEnumerator Upload()
    {
        WWWForm form = new WWWForm();
        form.AddField("myField", "myData");

        using (UnityWebRequest www = UnityWebRequest.Post(imageURL, form))
        {
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
            }
            else
            {
                Debug.Log("Form upload complete!");
            }
        }
    }
   
    public void UploadImage(Texture2D tex)
    {
        StartCoroutine(UploadPNG(tex));
    }

    IEnumerator UploadPNG(Texture2D tex)
    {
        // Read the screen after all rendering is complete
        yield return new WaitForEndOfFrame();

        // Encode texture into PNG
        byte[] bytes = tex.EncodeToPNG();
        Destroy(tex);

        // Create a Web Form
        WWWForm form = new WWWForm();
        
        form.AddBinaryData("avatar", bytes, "doodleImage.png", "image/png");

        // Upload to server script
        using (var w = UnityWebRequest.Post(imageURL, form))
        {
            yield return w.SendWebRequest();
            if (w.isNetworkError || w.isHttpError)
            {
                print(w.error);
            }
            else
            {
                print("Finished Uploading Screenshot");
            }
        }
    }
}

