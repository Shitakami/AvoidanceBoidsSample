using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class VectorFieldRenderer : MonoBehaviour
{

    [Header("DrawMeshInstancedInDirectの項目")]
    private Mesh m_mesh;

    [SerializeField]
    private Material m_material;
    [SerializeField]
    private Bounds m_bounds;

    private ComputeBuffer m_argsBuffer;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
