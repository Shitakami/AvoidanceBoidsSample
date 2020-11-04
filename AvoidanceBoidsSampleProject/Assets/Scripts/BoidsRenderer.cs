using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BoidsRenderer : MonoBehaviour
{
    [Header("DrawMeshInstancedIndirectのパラメータ")]
    [SerializeField]
    private Mesh m_mesh;

    [SerializeField]
    private Material m_material;

    [SerializeField]
    private Bounds m_bounds;

    [SerializeField]
    private ShadowCastingMode m_shadowCastingMode;

    [SerializeField]
    private bool m_receiveShadows;

    private AvoidanceBoids m_avoidanceBoids;

    private ComputeBuffer m_argsBuffer;


    public void Initialize(AvoidanceBoids avoidanceBoids) {
        m_avoidanceBoids = avoidanceBoids;
        InitializeArgsBuffer();

        m_material.SetBuffer("boidsDataBuffer", m_avoidanceBoids.BoidsDataBuffer);

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
            m_shadowCastingMode,
            m_receiveShadows
        );
    }


    private void InitializeArgsBuffer() {

        var args = new uint[] { 0, 0, 0, 0, 0 };

        args[0] = m_mesh.GetIndexCount(0);
        args[1] = (uint)m_avoidanceBoids.BoidsInstanceCount;

        m_argsBuffer = new ComputeBuffer(1, 4 * args.Length, ComputeBufferType.IndirectArguments);

        m_argsBuffer.SetData(args);

    }


    private void OnDestroy() {

        m_argsBuffer?.Release();

    }

}
