using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

public class AvoidanceBoids : MonoBehaviour
{
    [SerializeField]
    private ComputeShader m_avoidanceBoidsSimulator;

    [SerializeField]
    private SkinnedMeshRenderer[] m_skinnedMeshRenderer;
    private Transform[] m_skinnedMeshTransform; 

    [Header("ベクトル場のマス目数")]
    [SerializeField]
    private int m_vectorFieldLength;

    [Header("ベクトル場のマスの大きさ")]
    [SerializeField]
    private float m_squareScale;

    [Header("ベクトル場の中心座標")]
    [SerializeField]
    private Vector3 m_vectorFieldCenter;

    private Mesh m_bakeMesh;
    private List<Vector3> m_vertices = new List<Vector3>();
    private List<Vector3> m_normals = new List<Vector3>();

    private Vector4[] m_meshPositions;
    private Vector4[] m_meshEulerAngles;
    private Vector4[] m_meshScales;
    private int[] m_vertexCountThresholds;

    #region ComputeShaderKernelAndGroupSize
    private int m_initializeVectorFieldKernel;
    private Vector3Int m_initializeVectorFieldGroupSize;

    private int m_updateVectorFieldKernel;
    private Vector3Int m_updateVectorFieldGroupSize;
    #endregion

    #region ComputeBuffers
    private ComputeBuffer m_vectorFieldBuffer;
    private ComputeBuffer m_positionBuffer;
    private ComputeBuffer m_normalBuffer;
    #endregion

    #region Shader_PropertyID
    private readonly int m_positionPropID = Shader.PropertyToID("_PositionBuffer");
    private readonly int m_normalPropID = Shader.PropertyToID("_NormalBuffer");
    private readonly int m_modelPositionPropID = Shader.PropertyToID("_ModelPosition");
    private readonly int m_modelEulerAnglePropID = Shader.PropertyToID("_ModelEulerAngle");
    private readonly int m_modelScalePropID = Shader.PropertyToID("_ModelScale");
    #endregion

    #region Properties
    public ComputeBuffer VectorField { get { return m_vectorFieldBuffer; } }
    public float SquareScale { get { return m_squareScale; } }
    public int SquareCount { get { return m_vectorFieldLength*m_vectorFieldLength*m_vectorFieldLength; } }
    
    public ComputeBuffer BoidsDataBuffer { get { return m_boidsDataBuffer; } }
    public int BoidsInstanceCount { get { return m_instanceCount; } }
    #endregion

    // ベクトル場のマス情報
    private struct VectorFieldSquare {
        public Vector3 position;
        public Vector3 direction;
    }

    [Space(20)]
    [SerializeField]
    private int m_instanceCount;

    [Header("力の強さ")]
    [Header("Boidsモデルのデータ")]
    [SerializeField]
    private float m_cohesionForce;
    [SerializeField]
    private float m_separationForce;
    [SerializeField]
    private float m_alignmentForce;

    [Space(5)]
    [Header("力の働く距離")]
    [SerializeField]
    private float m_cohesionDistance;
    [SerializeField]
    private float m_separationDistance;
    [SerializeField]
    private float m_alignmentDistance;

    [Space(5)]
    [Header("力の働く角度")]
    [SerializeField]
    private float m_cohesionAngle;
    [SerializeField]
    private float m_separationAngle;
    [SerializeField]
    private float m_alignmentAngle;

    [Space(5)]
    [SerializeField]
    private float m_boundaryForce;
    [SerializeField]
    private float m_boundaryRange;
    [SerializeField]
    private Vector3 m_boundaryCenter;

    [SerializeField]
    private float m_avoidForce;

    [Space(5)]
    [SerializeField]
    private float m_maxVelocity;

    [SerializeField]
    private float m_minVelocity;

    [SerializeField]    
    private float m_maxForce;

    private struct BoidsData {
        public Vector3 position;
        public Vector3 velocity;
    }

    private ComputeBuffer m_boidsDataBuffer;

    private int m_updateBoidsKernel;

    private int m_deltaTimeID = Shader.PropertyToID("_DeltaTime");

    private Vector3Int m_updateBoidsGroupSize;

    [Header("レンダラー")]
    [Space(20)]
    [SerializeField]
    private BoxRenderer m_boxRenderer;
    [SerializeField]
    private BoidsRenderer m_boidsRenderer;

    // Start is called before the first frame update
    void Start()
    {
        
        m_meshPositions = new Vector4[m_skinnedMeshRenderer.Length];
        m_meshEulerAngles = new Vector4[m_skinnedMeshRenderer.Length];
        m_meshScales = new Vector4[m_skinnedMeshRenderer.Length];
        m_vertexCountThresholds = new int[m_skinnedMeshRenderer.Length];

        InitializeAvoidanceBoids();
        InitializeBoidsSimulator();

        // GC.Allocを防ぐため
        m_bakeMesh = new Mesh();

        m_skinnedMeshTransform = new Transform[m_skinnedMeshRenderer.Length];
        for(int i = 0; i < m_skinnedMeshRenderer.Length; ++i)
            m_skinnedMeshTransform[i] = m_skinnedMeshRenderer[i].transform;
        // m_skinnedMeshTransform = m_skinnedMeshRenderer.transform;

        // AvoidanceBoidsの初期化が完了したらRendererを初期化する
        m_boxRenderer?.Initialize(this);
        m_boidsRenderer?.Initialize(this);

    }

    // Update is called once per frame
    void Update()
    {
        UpdateVectorField();
        UpdateBoids();
    }

    private void InitializeAvoidanceBoids() {

        // スレッドサイズ取得に使用
        uint x, y, z;

        // すべてのSkinnedMeshRendererの頂点数を求める
        // また頂点数の累積和を求めてComputeShaderの計算で使用する
        int vertexCount = 0;
        for(int i = 0; i < m_skinnedMeshRenderer.Length; ++i) {
            vertexCount += m_skinnedMeshRenderer[i].sharedMesh.vertexCount;
            m_vertexCountThresholds[i] = vertexCount;
        }
        // int vertexCount = m_skinnedMeshRenderer.sharedMesh.vertexCount;

        InitializeVectorFieldBuffer();
        InitializePositionNormalBuffer(vertexCount);

        m_initializeVectorFieldKernel = m_avoidanceBoidsSimulator.FindKernel("InitializeVectorField");
        m_avoidanceBoidsSimulator.GetKernelThreadGroupSizes(m_initializeVectorFieldKernel, out x, out y, out z);
        int squaresCount = m_vectorFieldLength*m_vectorFieldLength*m_vectorFieldLength;
        m_initializeVectorFieldGroupSize = new Vector3Int(squaresCount/(int)x, (int)y, (int)z);
        m_avoidanceBoidsSimulator.SetBuffer(m_initializeVectorFieldKernel, "_VectorFieldBuffer", m_vectorFieldBuffer);

        m_updateVectorFieldKernel = m_avoidanceBoidsSimulator.FindKernel("UpdateVectorField");
        m_avoidanceBoidsSimulator.GetKernelThreadGroupSizes(m_updateVectorFieldKernel, out x, out y, out z);
        
        m_updateVectorFieldGroupSize = new Vector3Int(vertexCount/(int)x, (int)y, (int)z);

        m_avoidanceBoidsSimulator.SetBuffer(m_updateVectorFieldKernel, "_PositionBuffer", m_positionBuffer);
        m_avoidanceBoidsSimulator.SetBuffer(m_updateVectorFieldKernel, "_NormalBuffer", m_normalBuffer);
        m_avoidanceBoidsSimulator.SetBuffer(m_updateVectorFieldKernel, "_VectorFieldBuffer", m_vectorFieldBuffer);
        m_avoidanceBoidsSimulator.SetFloat("_SquareScale", m_squareScale);
        m_avoidanceBoidsSimulator.SetInt("_VectorFieldLength", m_vectorFieldLength);

        float halfLength = m_squareScale * m_vectorFieldLength/2.0f;
        m_avoidanceBoidsSimulator.SetVector("_MinField", m_vectorFieldCenter - new Vector3(halfLength, halfLength, halfLength));
        m_avoidanceBoidsSimulator.SetVector("_MaxField", m_vectorFieldCenter + new Vector3(halfLength, halfLength, halfLength));

    }


    private void InitializeVectorFieldBuffer() {

        int squaresCount = m_vectorFieldLength*m_vectorFieldLength*m_vectorFieldLength;

        VectorFieldSquare[] squares = new VectorFieldSquare[squaresCount];

        float startPosX = m_vectorFieldCenter.x - m_vectorFieldLength * m_squareScale / 2.0f + m_squareScale / 2.0f;
        float startPosY = m_vectorFieldCenter.y - m_vectorFieldLength * m_squareScale / 2.0f + m_squareScale / 2.0f;
        float startPosZ = m_vectorFieldCenter.z - m_vectorFieldLength * m_squareScale / 2.0f + m_squareScale / 2.0f;

        for(int i = 0; i < m_vectorFieldLength; ++i) {
            for(int j = 0; j < m_vectorFieldLength; ++j) {
                for(int k = 0; k < m_vectorFieldLength; ++k) {
                    int index = i*m_vectorFieldLength*m_vectorFieldLength + j*m_vectorFieldLength + k;
                    squares[index].position = new Vector3(startPosX + i * m_squareScale, startPosY + j * m_squareScale, startPosZ + k * m_squareScale);
                    squares[index].direction = Vector3.zero;
                }
            }
        }

        m_vectorFieldBuffer = new ComputeBuffer(squaresCount, Marshal.SizeOf(typeof(VectorFieldSquare)));
        m_vectorFieldBuffer.SetData(squares);

    }

    private void InitializePositionNormalBuffer(int vertexCount) {

        m_positionBuffer = new ComputeBuffer(vertexCount, Marshal.SizeOf(typeof(Vector3)));
        m_normalBuffer = new ComputeBuffer(vertexCount, Marshal.SizeOf(typeof(Vector3)));

    }

    private void InitializeBoidsDataBuffer() {

        var boidsData = new BoidsData[m_instanceCount];

        for(int i = 0; i < m_instanceCount; ++i) {
            boidsData[i].position = Random.insideUnitSphere * m_boundaryRange;
            var velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            boidsData[i].velocity = velocity.normalized * m_minVelocity; 
        }

        m_boidsDataBuffer = new ComputeBuffer(m_instanceCount, Marshal.SizeOf(typeof(BoidsData)));
        m_boidsDataBuffer.SetData(boidsData);


    }

    private void InitializeBoidsSimulator() {

        // 生成する個体の数を2の累乗にする（計算しやすくするため）
        m_instanceCount = Mathf.ClosestPowerOfTwo(m_instanceCount);

        InitializeBoidsDataBuffer();

        m_updateBoidsKernel = m_avoidanceBoidsSimulator.FindKernel("UpdateBoids");

        m_avoidanceBoidsSimulator.GetKernelThreadGroupSizes(m_updateBoidsKernel, out uint x, out uint y, out uint z);
        m_updateBoidsGroupSize = new Vector3Int(m_instanceCount / (int)x, (int)y, (int)z);

        m_avoidanceBoidsSimulator.SetFloat("_CohesionForce", m_cohesionForce);
        m_avoidanceBoidsSimulator.SetFloat("_SeparationForce", m_separationForce);
        m_avoidanceBoidsSimulator.SetFloat("_AlignmentForce", m_alignmentForce);

        // ComputeShader内の距離判定で2乗の値を使用しているので合わせる
        m_avoidanceBoidsSimulator.SetFloat("_CohesionDistance", m_cohesionDistance);
        m_avoidanceBoidsSimulator.SetFloat("_SeparationDistance", m_separationDistance);
        m_avoidanceBoidsSimulator.SetFloat("_AlignmentDistance", m_alignmentDistance);

        // ComputeShader内ではラジアンで判定するので度数方からラジアンに変更する
        m_avoidanceBoidsSimulator.SetFloat("_CohesionAngle", m_cohesionAngle * Mathf.Deg2Rad);
        m_avoidanceBoidsSimulator.SetFloat("_SeparationAngle", m_separationAngle * Mathf.Deg2Rad);
        m_avoidanceBoidsSimulator.SetFloat("_AlignmentAngle", m_alignmentAngle * Mathf.Deg2Rad);

        m_avoidanceBoidsSimulator.SetFloat("_BoundaryForce", m_boundaryForce);
        m_avoidanceBoidsSimulator.SetFloat("_BoundaryRange", m_boundaryRange * m_boundaryRange);
        m_avoidanceBoidsSimulator.SetVector("_BoundaryCenter", m_boundaryCenter);
        m_avoidanceBoidsSimulator.SetFloat("_AvoidForce", m_avoidForce);

        m_avoidanceBoidsSimulator.SetFloat("_MinVelocity", m_minVelocity);
        m_avoidanceBoidsSimulator.SetFloat("_MaxVelocity", m_maxVelocity);

        m_avoidanceBoidsSimulator.SetInt("_InstanceCount", m_instanceCount);

        m_avoidanceBoidsSimulator.SetFloat("_MaxForce", m_maxForce);
        
        m_avoidanceBoidsSimulator.SetBuffer(m_updateBoidsKernel, "_BoidsData", m_boidsDataBuffer);
        m_avoidanceBoidsSimulator.SetBuffer(m_updateBoidsKernel, "_VectorFieldBuffer", m_vectorFieldBuffer);

        m_avoidanceBoidsSimulator.SetFloat("_Epsilon", Mathf.Epsilon);
    }

    /// <summary>
    /// ベクトル場の更新
    /// </summary>
    private void UpdateVectorField() {
        // ベクトル場の初期化
        m_avoidanceBoidsSimulator.Dispatch(m_initializeVectorFieldKernel,
                                           m_initializeVectorFieldGroupSize.x,
                                           m_initializeVectorFieldGroupSize.y,
                                           m_initializeVectorFieldGroupSize.z);
        
        int bufferIndex = 0;
        for(int i = 0; i < m_skinnedMeshRenderer.Length; ++i) {
            m_meshPositions[i] = m_skinnedMeshTransform[i].position;
            m_meshEulerAngles[i] = m_skinnedMeshTransform[i].eulerAngles;
            m_meshScales[i] = m_skinnedMeshTransform[i].localScale;
            m_skinnedMeshRenderer[i].BakeMesh(m_bakeMesh);
            m_bakeMesh.GetVertices(m_vertices);
            m_bakeMesh.GetNormals(m_normals);
            int vertexCount = m_skinnedMeshRenderer[i].sharedMesh.vertexCount;
            m_positionBuffer.SetData(m_vertices, 0, bufferIndex, vertexCount);
            m_normalBuffer.SetData(m_normals, 0, bufferIndex, vertexCount);
            bufferIndex += vertexCount;
        }

        m_avoidanceBoidsSimulator.SetVectorArray(m_modelPositionPropID, m_meshPositions);
        m_avoidanceBoidsSimulator.SetVectorArray(m_modelEulerAnglePropID, m_meshEulerAngles);
        m_avoidanceBoidsSimulator.SetVectorArray(m_modelScalePropID, m_meshScales);

        // m_avoidanceBoidsSimulator.SetVector(m_modelPositionPropID, m_skinnedMeshTransform.position);
        // m_avoidanceBoidsSimulator.SetVector(m_modelEulerAnglePropID, m_skinnedMeshTransform.eulerAngles);
        // m_avoidanceBoidsSimulator.SetVector(m_modelScalePropID, m_skinnedMeshTransform.localScale);
        m_avoidanceBoidsSimulator.Dispatch(m_updateVectorFieldKernel,
                                           m_updateVectorFieldGroupSize.x,
                                           m_updateVectorFieldGroupSize.y,
                                           m_updateVectorFieldGroupSize.z);

    }

    /// <summary>
    /// Boidsアルゴリズムの更新
    /// </summary>
    private void UpdateBoids() {

        m_avoidanceBoidsSimulator.SetFloat(m_deltaTimeID, Time.deltaTime);
        m_avoidanceBoidsSimulator.Dispatch(m_updateBoidsKernel,
                                           m_updateBoidsGroupSize.x,
                                           m_updateBoidsGroupSize.y,
                                           m_updateBoidsGroupSize.z);

    }

    void OnDestroy() {

        m_vectorFieldBuffer?.Release();
        m_positionBuffer?.Release();
        m_normalBuffer?.Release();
        m_boidsDataBuffer?.Release();

    }

}
