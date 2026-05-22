using UnityEngine;

public class GameUIScriot : MonoBehaviour
{
    public static GameUIScriot Instance;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(!Instance)
            Instance = new GameUIScriot();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
