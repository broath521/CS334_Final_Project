using Assets.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TerrainUtils;

public class ChangeView : MonoBehaviour
{
    float target;
    float scale = 1;
    public float moveSpeed = 3.0f;
    public float zoomSpeed = 30.0f;
    public float zoomOffset = 0.1f;
    public float minZoom, maxZoom;

    private Camera camera;

    TilemapStructure tilemapStructure;
    [SerializeField] GameObject terrainMap;
    private void Awake()
    {
        tilemapStructure = terrainMap.GetComponent<TilemapStructure>();
        camera = GetComponent<Camera>();
    }

    // Start is called before the first frame update
    void Start()
    {
        camera.transform.position = new Vector3(tilemapStructure.Width/2, tilemapStructure.Height/2, -10);
        target = camera.orthographicSize; 
    }

    // Update is called once per frame
    void Update()
    {
        //camera movement
        if (Input.GetKey(KeyCode.D))
        {
            camera.transform.Translate(new Vector3(moveSpeed * Time.deltaTime, 0, 0));
        }
        if (Input.GetKey(KeyCode.A))
        {
            camera.transform.Translate(new Vector3(-moveSpeed * Time.deltaTime, 0, 0));
        }
        if (Input.GetKey(KeyCode.S))
        {
            camera.transform.Translate(new Vector3(0, -moveSpeed * Time.deltaTime, 0));
        }
        if (Input.GetKey(KeyCode.W))
        {
            camera.transform.Translate(new Vector3(0, moveSpeed * Time.deltaTime, 0));
        }

        //camera zooming
        if(Input.GetKey(KeyCode.LeftControl))
        {
            target -= zoomOffset;
            target = Mathf.Clamp(target, minZoom, maxZoom);
            camera.orthographicSize = Mathf.MoveTowards(camera.orthographicSize, target, zoomSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.Space))
        {
            target += zoomOffset;
            target = Mathf.Clamp(target, minZoom, maxZoom);
            camera.orthographicSize = Mathf.MoveTowards(camera.orthographicSize, target, zoomSpeed * Time.deltaTime);
        }
    }
}
