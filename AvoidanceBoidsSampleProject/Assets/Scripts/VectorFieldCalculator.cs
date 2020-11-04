using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
public class VectorFieldCalculator : MonoBehaviour
{

    [SerializeField]
    private ComputeShader m_vectorGridCalculator;

    private Vector3Int m_calcVectorGridGroupSize;
    private Vector3Int m_initializeGridGroupSize;
    private int m_calcVectorGridKernel;
    private int m_initializeGridKernel;

    [SerializeField]
    private SkinnedMeshRenderer m_skinnedMeshRenderer;

    [Space(20)]
    [SerializeField]
    private float m_boxScale;

    [SerializeField]
    private int m_boxLength;

    [SerializeField]
    private Vector3 m_boxCenter;

    [Header("DrawMeshInstancedInDirectの項目")]
    [Space(20)]
    [SerializeField]
    private Mesh m_mesh;

    [SerializeField]
    private Material m_material;

    [SerializeField]
    private Bounds m_bounds;

    private ComputeBuffer m_argsBuffer;
    private ComputeBuffer m_boxDataBuffer;
    private ComputeBuffer m_normalsBuffer;
    private ComputeBuffer m_positionsBuffer;
    private List<Vector3> m_vertices = new List<Vector3>(); // GC.Collectを防ぐため
    private List<Vector3> m_normals = new List<Vector3>();  // GC.Collectを防ぐため
    private Mesh m_bakeMesh;
    private Transform m_modelTransform;
    private readonly int m_normalsPropID = Shader.PropertyToID("normals");
    private readonly int m_positionsPropID = Shader.PropertyToID("positions");
    private readonly int m_modelRotationPropID = Shader.PropertyToID("modelRotation");
    private readonly int m_modelEulerAnglePropID = Shader.PropertyToID("modelEulerAngle");
    private readonly int m_modelLocalScale = Shader.PropertyToID("modelLocalScale");
    private readonly int m_modelPositionPropID = Shader.PropertyToID("modelPosition");

    
    struct BoxData {
        public Vector3 position;
        public Vector3 direction;
    }


    // Start is called before the first frame update
    void Start()
    {
        InitializeArgsBuffer();
        InitializeVectorGridCalculator();
        
        m_material.SetFloat("_Scale", m_boxScale);

        m_modelTransform = m_skinnedMeshRenderer.transform;

        // GC.Allocを防ぐため
        m_bakeMesh = new Mesh();
    }

    // Update is called once per frame
    void Update()
    {

        UpdateVectorField();

    }

    void LateUpdate() {

        Graphics.DrawMeshInstancedIndirect(
            m_mesh,
            0,
            m_material,
            m_bounds,
            m_argsBuffer,
            0,
            null,
            ShadowCastingMode.Off,
            false
        );

    }

    public void UpdateVectorField() {

        // ベクトル場の初期化
        m_vectorGridCalculator.Dispatch(m_initializeGridKernel, 
                                       m_initializeGridGroupSize.x, 
                                       m_initializeGridGroupSize.y, 
                                       m_initializeGridGroupSize.z);
        
        m_skinnedMeshRenderer.BakeMesh(m_bakeMesh);
        m_bakeMesh.GetVertices(m_vertices);
        m_bakeMesh.GetNormals(m_normals);

        m_normalsBuffer.SetData(m_normals);
        m_positionsBuffer.SetData(m_vertices);

        m_vectorGridCalculator.SetVector(m_modelLocalScale, m_modelTransform.localScale);
        m_vectorGridCalculator.SetVector(m_modelEulerAnglePropID, m_modelTransform.eulerAngles);
        m_vectorGridCalculator.SetVector(m_modelPositionPropID, m_modelTransform.position);
        m_vectorGridCalculator.Dispatch(m_calcVectorGridKernel,
                                       m_calcVectorGridGroupSize.x,
                                       m_calcVectorGridGroupSize.y,
                                       m_calcVectorGridGroupSize.z);

    }

    private void InitializeArgsBuffer() {

        uint[] args = new uint[5] {0, 0, 0, 0, 0};

        args[0] = m_mesh.GetIndexCount(0);
        args[1] = (uint)(m_boxLength * m_boxLength * m_boxLength);

        m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        m_argsBuffer.SetData(args);
    }

    private void InitializeBoxDataBuffer() {

        int boxesCount = m_boxLength * m_boxLength * m_boxLength;
        BoxData[] boxDatas = new BoxData[boxesCount];
        float startPosX = m_boxCenter.x - m_boxLength * m_boxScale / 2.0f + m_boxScale/2.0f;
        float startPosY = m_boxCenter.y - m_boxLength * m_boxScale / 2.0f + m_boxScale/2.0f;
        float startPosZ = m_boxCenter.z - m_boxLength * m_boxScale / 2.0f + m_boxScale/2.0f;

        for(int i = 0; i < m_boxLength; ++i) {
            for(int j = 0; j < m_boxLength; ++j) {
                for(int k = 0; k < m_boxLength; ++k) {
                    int index = i * m_boxLength * m_boxLength + j * m_boxLength + k;
                    boxDatas[index].position = new Vector3(startPosX + i * m_boxScale, startPosY + j * m_boxScale, startPosZ + k * m_boxScale);
                    boxDatas[index].direction = Vector3.zero;
                }
            }
        }

        m_boxDataBuffer = new ComputeBuffer(boxesCount, Marshal.SizeOf(typeof(BoxData)));
        m_boxDataBuffer.SetData(boxDatas);
        m_material.SetBuffer("_BoxDataBuffer", m_boxDataBuffer);
    }

    private void InitializeVectorGridCalculator() {

        InitializeBoxDataBuffer();

        uint x, y, z;

        m_initializeGridKernel = m_vectorGridCalculator.FindKernel("InitializeGrid");
        m_vectorGridCalculator.GetKernelThreadGroupSizes(m_initializeGridKernel, out x, out y, out z);        
        m_initializeGridGroupSize = new Vector3Int(m_boxLength*m_boxLength*m_boxLength/(int)x, (int)y, (int)z);
        m_vectorGridCalculator.SetBuffer(m_initializeGridKernel, "boxData", m_boxDataBuffer);

        m_calcVectorGridKernel = m_vectorGridCalculator.FindKernel("CalcVectorGrid");

        m_vectorGridCalculator.GetKernelThreadGroupSizes(m_calcVectorGridKernel, out x, out y, out z);
        m_calcVectorGridGroupSize = new Vector3Int(m_skinnedMeshRenderer.sharedMesh.vertexCount / (int)x, (int)y, (int)z);

        float halfLength = m_boxScale * m_boxLength/2.0f;

        Vector3 minField = m_boxCenter - new Vector3(halfLength, halfLength, halfLength);
        Vector3 maxField = m_boxCenter + new Vector3(halfLength, halfLength, halfLength);

        m_vectorGridCalculator.SetVector("minField", minField);
        m_vectorGridCalculator.SetVector("maxField", maxField);

        m_vectorGridCalculator.SetFloat("boxScale", m_boxScale);
        m_vectorGridCalculator.SetInt("boxLength", m_boxLength); 

        m_vectorGridCalculator.SetBuffer(m_calcVectorGridKernel,
                                        "boxData",
                                        m_boxDataBuffer);

        m_normalsBuffer = new ComputeBuffer(m_skinnedMeshRenderer.sharedMesh.vertexCount, Marshal.SizeOf(typeof(Vector3)));
        m_positionsBuffer = new ComputeBuffer(m_skinnedMeshRenderer.sharedMesh.vertexCount, Marshal.SizeOf(typeof(Vector3)));
        m_vectorGridCalculator.SetBuffer(m_calcVectorGridKernel, m_normalsPropID, m_normalsBuffer);
        m_vectorGridCalculator.SetBuffer(m_calcVectorGridKernel, m_positionsPropID, m_positionsBuffer);
    }

    void OnDestroy() {
        m_argsBuffer?.Release();
        m_boxDataBuffer?.Release();
        m_normalsBuffer?.Release();
        m_positionsBuffer?.Release();
    }

}
