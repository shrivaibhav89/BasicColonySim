using UnityEngine;
using UnityEngine.Networking;

public class testScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public async void LoadDataFromAPI(string apiUrl)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            await request.SendWebRequest();
            if(request.result == UnityWebRequest.Result.Success)
            {
                string jsondata = request.downloadHandler.text;
                Debug.Log("Data received: " + jsondata);
            }
            else
            {
                Debug.LogError("Error fetching data: " + request.error);
            }
        }
        
    }

   
}
