using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ParticleEffects
{

    [System.Serializable]
    public class ParticleMeshData
    {
        public List<int> neibourghIndices = new List<int>();

        int actualIndex = 0;
        public bool HasNeibourgh(int index)
        {
            for (int i = 0; i < neibourghIndices.Count; i++)
            {
                if (neibourghIndices[i] == index)
                {
                    return true;
                }
            }
            return false;
        }

        public void AddNeibourgh(int index)
        {
            if (actualIndex >= neibourghIndices.Count)
            {
                neibourghIndices.Add(index);
            }
            else
            {
                neibourghIndices[actualIndex] = index;
            }

            ++actualIndex;
        }

        public int NeibourghCount
        {
            get
            {
                return actualIndex;
            }
        }

        public void ClearData()
        {
            actualIndex = 0;
            for (int i = 0; i < neibourghIndices.Count; i++)
            {
                neibourghIndices[i] = -1;
            }
        }
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(ParticleSystem))]
    public class PlexusEffect : MonoBehaviour
    {

        [Header("Lines")]
        public LineRenderer m_LinePrefab;
        public float m_SearchDst;
        public int m_MaxLinesCount;
        public int m_MaxLinesPerParticle;

        [Range(0f, 1f)]
        public float m_LineColorFromParticle;

        [Range(0f, 1f)]
        public float m_LineSizeFromParticle;

        [Header("Mesh")]
        public bool m_UseMesh;
        public int m_MaxTrianglesCount;
        public int m_ComparisonJump = 2;
        [Range(0f, 1f)]
        public float m_MeshColorFromParticle;

        //Particle Things
        ParticleSystem m_PS;
        ParticleSystem.MainModule m_ParticleMainModule;
        ParticleSystem.Particle[] m_Particles;

        //Mesh things
        MeshFilter m_MeshFilter;
        Mesh m_Mesh;
        List<Vector3> m_Verticies = new List<Vector3>();
        List<int> m_Triangles = new List<int>();
        List<Color> m_VertexColors = new List<Color>();

        //Simulation space things
        Transform m_SimulationTransform;
        Matrix4x4 m_SimulationTransformMatrix;

        //Other things...
        List<LineRenderer> m_LinePool = new List<LineRenderer>();

        ParticleMeshData[] m_ParticleMeshData;

        int m_VertexIndex;
        int m_LineIndex;

        int m_ParticleCount;

        Color[] m_LineColors;
        float[] m_LineWidths;

        private void Awake()
        {
            m_PS = GetComponent<ParticleSystem>();
            m_MeshFilter = GetComponent<MeshFilter>();

            m_ParticleMainModule = m_PS.main;

            m_Mesh = new Mesh();

            m_LineColors = new Color[2];
            m_LineColors[0] = m_LinePrefab.startColor;
            m_LineColors[1] = m_LinePrefab.endColor;

            m_LineWidths = new float[2];
            m_LineWidths[0] = m_LinePrefab.startWidth;
            m_LineWidths[1] = m_LinePrefab.endWidth;

        }

        private void LateUpdate()
        {

            m_Verticies.Clear();
            m_Triangles.Clear();
            m_VertexColors.Clear();

            CreateOrUpdateArrays();

            m_PS.GetParticles(m_Particles);
            m_ParticleCount = m_PS.particleCount;

            m_VertexIndex = 0;
            m_LineIndex = 0;

            if (m_UseMesh)
            {
                for (int i = 0; i < m_ParticleCount; i++)
                {
                    m_ParticleMeshData[i].ClearData();
                }
            }

            switch (m_ParticleMainModule.simulationSpace)
            {
                case ParticleSystemSimulationSpace.Local:
                    {
                        m_SimulationTransform = transform;
                        break;
                    }
                case ParticleSystemSimulationSpace.World:
                    {
                        m_SimulationTransform = transform;
                        m_SimulationTransformMatrix = transform.worldToLocalMatrix;
                        break;

                    }
                case ParticleSystemSimulationSpace.Custom:
                    {
                        m_SimulationTransform = m_ParticleMainModule.customSimulationSpace;
                        m_SimulationTransformMatrix = m_SimulationTransform.localToWorldMatrix * transform.worldToLocalMatrix;
                        break;
                    }
            }

            for (int i = 0; i < m_ParticleCount; i++)
            {

                ParticleSystem.Particle firstParticle = m_Particles[i];
                int actualLinesCount = 0;

                if (m_LineIndex >= m_MaxLinesCount)
                {
                    break;
                }

                for (int j = i + 1; j < m_ParticleCount; j++)
                {
                    if (actualLinesCount >= m_MaxLinesPerParticle || m_LineIndex >= m_MaxLinesCount)
                    {
                        break;
                    }

                    ParticleSystem.Particle secondParticle = m_Particles[j];

                    float particleSqrDst = (firstParticle.position - secondParticle.position).sqrMagnitude;

                    if (particleSqrDst < m_SearchDst * m_SearchDst)
                    {
                        LineRenderer line = GetNextLine();

                        line.useWorldSpace = m_ParticleMainModule.simulationSpace == ParticleSystemSimulationSpace.World;

                        line.SetPosition(0, firstParticle.position);
                        line.SetPosition(1, secondParticle.position);

                        if (m_LineSizeFromParticle > 0)
                        {
                            line.startWidth = Mathf.Lerp(m_LineWidths[0], firstParticle.GetCurrentSize(m_PS), m_LineSizeFromParticle);
                            line.endWidth = Mathf.Lerp(m_LineWidths[1], secondParticle.GetCurrentSize(m_PS), m_LineSizeFromParticle);
                        }

                        if (m_LineColorFromParticle > 0)
                        {
                            line.startColor = Color.Lerp(m_LineColors[0], firstParticle.GetCurrentColor(m_PS), m_LineColorFromParticle);
                            line.endColor = Color.Lerp(m_LineColors[1], secondParticle.GetCurrentColor(m_PS), m_LineColorFromParticle);
                        }

                        ++m_LineIndex;
                        ++actualLinesCount;

                        if (m_UseMesh)
                        {
                            m_ParticleMeshData[i].AddNeibourgh(j);
                        }
                    }
                }
            }

            //Hide unused lines
            for (int i = m_LineIndex; i < m_LinePool.Count; i++)
            {
                m_LinePool[i].gameObject.SetActive(false);
            }

            if (m_UseMesh)
            {
                SetUpMesh();
            }
            else
            {
                m_Mesh.Clear();
            }
        }

        private void CreateOrUpdateArrays()
        {
            int maxParticles = m_ParticleMainModule.maxParticles;
            if (m_Particles == null || m_Particles.Length < maxParticles)
            {
                m_Particles = new ParticleSystem.Particle[maxParticles];
            }

            if (m_UseMesh && (m_ParticleMeshData == null || m_ParticleMeshData.Length < maxParticles))
            {
                m_ParticleMeshData = new ParticleMeshData[maxParticles];
                for (int i = 0; i < m_ParticleMeshData.Length; i++)
                {
                    m_ParticleMeshData[i] = new ParticleMeshData();
                }
            }
        }

        void SetUpMesh()
        {
            CreateMeshFromParticleData();

            m_Mesh.Clear();

            m_Mesh.SetVertices(m_Verticies);
            m_Mesh.SetTriangles(m_Triangles, 0);
            m_Mesh.SetColors(m_VertexColors);

            m_Mesh.RecalculateNormals();

            m_MeshFilter.mesh = m_Mesh;
        }

        private void CreateMeshFromParticleData()
        {
            int trianglesCount = 0;

            for (int i = 0; i < m_ParticleCount; i++)
            {

                if (trianglesCount >= m_MaxTrianglesCount)
                {
                    return;
                }

                var neibourghs = m_ParticleMeshData[i].neibourghIndices;

                for (int j = 0; j < m_ParticleMeshData[i].NeibourghCount - 1; j++)
                {

                    int firstParticleIndex = neibourghs[j];
                    int secondParticleIndex = neibourghs[j + 1];

                    if (m_ParticleMeshData[firstParticleIndex].HasNeibourgh(secondParticleIndex) ||
                        m_ParticleMeshData[secondParticleIndex].HasNeibourgh(firstParticleIndex))
                    {

                        AddTriangle(i, firstParticleIndex, secondParticleIndex);
                        ++trianglesCount;
                    }
                }
            }
        }

        LineRenderer GetNextLine()
        {
            LineRenderer line;
            if (m_LineIndex >= m_LinePool.Count)
            {
                line = Instantiate(m_LinePrefab, m_SimulationTransform);
                m_LinePool.Add(line);
            }
            else
            {
                line = m_LinePool[m_LineIndex];
                line.gameObject.SetActive(true);
            }

            return line;
        }

        void AddTriangle(int indexA, int indexB, int indexC)
        {
            //Debug.Log("Added triangle");
            Vector3 vA = m_Particles[indexA].position;
            Vector3 vB = m_Particles[indexB].position;
            Vector3 vC = m_Particles[indexC].position;

            if (m_ParticleMainModule.simulationSpace != ParticleSystemSimulationSpace.Local)
            {
                vA = m_SimulationTransformMatrix.MultiplyPoint(vA);
                vB = m_SimulationTransformMatrix.MultiplyPoint(vB);
                vC = m_SimulationTransformMatrix.MultiplyPoint(vC);
            }

            m_Verticies.Add(vA);
            m_Verticies.Add(vB);
            m_Verticies.Add(vC);

            m_Triangles.Add(m_VertexIndex);
            m_Triangles.Add(m_VertexIndex + 1);
            m_Triangles.Add(m_VertexIndex + 2);

            if (m_MeshColorFromParticle > 0)
            {
                Color colorA = Color.Lerp(Color.white, m_Particles[indexA].GetCurrentColor(m_PS), m_MeshColorFromParticle);
                Color colorB = Color.Lerp(Color.white, m_Particles[indexB].GetCurrentColor(m_PS), m_MeshColorFromParticle);
                Color colorC = Color.Lerp(Color.white, m_Particles[indexC].GetCurrentColor(m_PS), m_MeshColorFromParticle);

                m_VertexColors.Add(colorA);
                m_VertexColors.Add(colorB);
                m_VertexColors.Add(colorC);
            }

            m_VertexIndex += 3;
        }
    }
}