using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BoxRenderer : MonoBehaviour
{
    [Header("DrawMeshInstancedInDirectの項目")]
    [Space(20)]
    [SerializeField]
    private Mesh _mesh;

    [SerializeField]
    private Material _material;

    [SerializeField]
    private Bounds _bounds;

    private ComputeBuffer _argsBuffer;

    private AvoidanceBoids m_avoidanceBoids;


    public void Initialize(AvoidanceBoids avoidanceBoids) {
        m_avoidanceBoids = avoidanceBoids;
        InitializeArgsBuffer();
        _material.SetFloat("_Scale", m_avoidanceBoids.SquareScale);
        _material.SetBuffer("_BoxDataBuffer", m_avoidanceBoids.VectorField);

    }

    void LateUpdate() {

            Graphics.DrawMeshInstancedIndirect(
            _mesh,
            0,
            _material,
            _bounds,
            _argsBuffer,
            0,
            null,
            ShadowCastingMode.Off,
            false
        );
    }

    private void InitializeArgsBuffer() {

        uint[] args = new uint[5] {0, 0, 0, 0, 0};

        args[0] = _mesh.GetIndexCount(0);
        int length = m_avoidanceBoids.SquareCount;
        args[1] = (uint)(length);

        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);
    }

    void OnDestroy() {

        _argsBuffer?.Release();

    }

}
