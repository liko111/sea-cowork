using UnityEngine;

public class RandomMaterialColor : MonoBehaviour
{
    private Material _material;

    void Start()
    {
        _material = GetComponent<Renderer>().material;
        SetRandomColors();
    }

    void SetRandomColors()
    {
        _material.SetColor("_Color1", Random.ColorHSV());
        _material.SetColor("_Color2", Random.ColorHSV());
    }
}