using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ParticleEffects {
    [RequireComponent(typeof(ParticleSystem))]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PlexusEffect : MonoBehaviour {

        //Simple type is more efficient but can create wrong effects with too little lines per particle
        public enum MeshCreationType { Simple, Complex }

        public class ParticleMeshData {
            public List<int> neibourghIndices = new List<int>();
        }

        [Header("Lines")]
        public LineRenderer m_LinePrefab;
        public float m_LineDst;
        public int m_MaxLines;
        public int m_MaxLinePerParticle;

        [Range(0f, 1f)]
        public float m_LineColourFromParticle;

        [Range(0f, 1f)]
        public float m_LineSizeFromParticle;

        [Header("Mesh")]
        public MeshCreationType m_MeshCreationType;
        public bool m_UseMesh;
        public int m_MaxTriangleCount;
        public int m_ComparisonJump = 2;
        [Range(0f, 1f)]
        public float m_MeshColorFromParticle;

        ParticleSystem m_PS;
        ParticleSystem.MainModule m_ParticleMainModule;
        ParticleSystem.Particle[] m_Particles;

        MeshFilter m_MeshFilter;
        Mesh m_Mesh;

        Transform m_SimulationTransform;

        List<LineRenderer> m_LinesPool = new List<LineRenderer>();

        List<Vector3> m_Verticies = new List<Vector3>();
        List<int> m_Triangles = new List<int>();
        List<Color> m_VertexColors = new List<Color>();

        List<ParticleMeshData> m_ParticleDataList = new List<ParticleMeshData>();

        int m_VertexIndex;

        int m_LineIndex;

        private void Awake() {
            m_PS = GetComponent<ParticleSystem>();
            m_MeshFilter = GetComponent<MeshFilter>();

            m_ParticleMainModule = m_PS.main;

            m_Mesh = new Mesh();
        }

        private void LateUpdate() {

            m_Verticies.Clear();
            m_Triangles.Clear();
            m_VertexColors.Clear();
            m_ParticleDataList.Clear();
            m_VertexIndex = 0;

            int maxParticles = m_ParticleMainModule.maxParticles;
            if (m_Particles == null || m_Particles.Length < maxParticles) {
                m_Particles = new ParticleSystem.Particle[maxParticles];
            }

            m_PS.GetParticles(m_Particles);
            int particlesCount = m_PS.particleCount;

            m_LineIndex = 0;
            int triangleCount = 0;

            switch (m_ParticleMainModule.simulationSpace) {
                case ParticleSystemSimulationSpace.Local: {
                        m_SimulationTransform = transform;
                        break;
                    }
                case ParticleSystemSimulationSpace.World: {
                        m_SimulationTransform = transform;
                        break;

                    }
                case ParticleSystemSimulationSpace.Custom: {
                        m_SimulationTransform = m_ParticleMainModule.customSimulationSpace;
                        break;
                    }
            }

            for (int i = 0; i < particlesCount; i++) {

                ParticleSystem.Particle firstParticle = m_Particles[i];
                int actualLinesCount = 0;
                int lastIndex = 0;

                m_ParticleDataList.Add(new ParticleMeshData());

                if (m_LineIndex >= m_MaxLines) {
                    break;
                }

                for (int j = i + 1; j < particlesCount; j++) {

                    ParticleSystem.Particle secondParticle = m_Particles[j];

                    float particleSqrDst = (firstParticle.position - secondParticle.position).sqrMagnitude;

                    if (particleSqrDst < m_LineDst * m_LineDst) {
                        LineRenderer line;
                        if (m_LineIndex >= m_LinesPool.Count) {
                            line = Instantiate(m_LinePrefab, m_SimulationTransform);
                            m_LinesPool.Add(line);
                        }
                        else {
                            line = m_LinesPool[m_LineIndex];
                            line.gameObject.SetActive(true);
                        }

                        line.useWorldSpace = m_ParticleMainModule.simulationSpace == ParticleSystemSimulationSpace.World;

                        line.SetPosition(0, firstParticle.position);
                        line.SetPosition(1, secondParticle.position);

                        if (m_LineSizeFromParticle > 0) {
                            line.startWidth = m_LinePrefab.startWidth * (1 - m_LineSizeFromParticle) + firstParticle.GetCurrentSize(m_PS) * m_LineSizeFromParticle;
                            line.endWidth = m_LinePrefab.endWidth * (1 - m_LineSizeFromParticle) + secondParticle.GetCurrentSize(m_PS) * m_LineSizeFromParticle;
                        }

                        if (m_LineColourFromParticle > 0) {
                            line.startColor = Color.Lerp(m_LinePrefab.startColor, firstParticle.GetCurrentColor(m_PS), m_LineColourFromParticle);
                            line.endColor = Color.Lerp(m_LinePrefab.endColor, secondParticle.GetCurrentColor(m_PS), m_LineColourFromParticle);
                        }

                        ++m_LineIndex;
                        ++actualLinesCount;

                        if (m_MeshCreationType == MeshCreationType.Simple) {
                            if (actualLinesCount % m_ComparisonJump == 0 && triangleCount < m_MaxTriangleCount && m_UseMesh) {
                                if (Vector3.SqrMagnitude(secondParticle.position - m_Particles[lastIndex].position) < m_LineDst * m_LineDst) {
                                    AddTriangle(i, j, lastIndex);
                                    triangleCount++;
                                }
                            }
                            lastIndex = j;

                        }
                        else {
                            m_ParticleDataList[i].neibourghIndices.Add(j);
                        }


                        if (actualLinesCount >= m_MaxLinePerParticle || m_LineIndex >= m_MaxLines) {
                            break;
                        }
                    }
                }
            }

            //Hide unused lines
            for (int i = m_LineIndex; i < m_LinesPool.Count; i++) {
                m_LinesPool[i].gameObject.SetActive(false);
            }

            if (m_UseMesh) {
                SetUpMesh();
            }
            else {
                m_Mesh.Clear();
            }
        }

        void SetUpMesh() {

            if (m_MeshCreationType == MeshCreationType.Complex) {
                CreateMeshFromParticleData();
            }

            m_Mesh.Clear();

            m_Mesh.SetVertices(m_Verticies);
            m_Mesh.triangles = m_Triangles.ToArray();
            m_Mesh.SetColors(m_VertexColors);

            m_Mesh.RecalculateNormals();

            m_MeshFilter.mesh = m_Mesh;
        }

        private void CreateMeshFromParticleData() {
            int trianglesCount = 0;

            for (int i = 0; i < m_ParticleDataList.Count; i++) {
                var neibourghs = m_ParticleDataList[i].neibourghIndices;
                for (int j = 0; j < neibourghs.Count - 1; j++) {
                    ParticleSystem.Particle firstParticle = m_Particles[neibourghs[j]];
                    ParticleSystem.Particle secondParticle = m_Particles[neibourghs[j + 1]];

                    float sqrDist = (firstParticle.position - secondParticle.position).sqrMagnitude;

                    if (sqrDist < m_LineDst * m_LineDst && j % m_ComparisonJump == 0) {
                        AddTriangle(i, neibourghs[j], neibourghs[j + 1]);
                        trianglesCount++;
                    }

                    if (trianglesCount > m_MaxTriangleCount) {
                        break;
                    }
                }

                if (trianglesCount > m_MaxTriangleCount) {
                    break;
                }
            }
        }

        void AddTriangle(int indexA, int indexB, int indexC) {
            Vector3 vA = m_Particles[indexA].position;
            Vector3 vB = m_Particles[indexB].position;
            Vector3 vC = m_Particles[indexC].position;

            //TODO: Zmienić na macierze
            if (m_ParticleMainModule.simulationSpace == ParticleSystemSimulationSpace.World) {
                vA = transform.InverseTransformPoint(vA);
                vB = transform.InverseTransformPoint(vB);
                vC = transform.InverseTransformPoint(vC);
            }
            else if (m_ParticleMainModule.simulationSpace == ParticleSystemSimulationSpace.Custom) {
                vA = m_SimulationTransform.TransformPoint(vA);
                vB = m_SimulationTransform.TransformPoint(vB);
                vC = m_SimulationTransform.TransformPoint(vC);

                vA = transform.InverseTransformPoint(vA);
                vB = transform.InverseTransformPoint(vB);
                vC = transform.InverseTransformPoint(vC);
            }

            m_Verticies.Add(vA);
            m_Verticies.Add(vB);
            m_Verticies.Add(vC);

            m_Triangles.Add(m_VertexIndex);
            m_Triangles.Add(m_VertexIndex + 1);
            m_Triangles.Add(m_VertexIndex + 2);

            Color colorA = Color.Lerp(Color.white, m_Particles[indexA].GetCurrentColor(m_PS), m_MeshColorFromParticle);
            Color colorB = Color.Lerp(Color.white, m_Particles[indexB].GetCurrentColor(m_PS), m_MeshColorFromParticle);
            Color colorC = Color.Lerp(Color.white, m_Particles[indexC].GetCurrentColor(m_PS), m_MeshColorFromParticle);

            m_VertexColors.Add(colorA);
            m_VertexColors.Add(colorB);
            m_VertexColors.Add(colorC);

            m_VertexIndex += 3;
        }
    }
}