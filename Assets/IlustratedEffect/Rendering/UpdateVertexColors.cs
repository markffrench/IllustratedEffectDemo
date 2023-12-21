using UnityEngine;

namespace bornacvitanic.Quantum.Rendering
{
    public class UpdateVertexColors : MonoBehaviour
    {
        [SerializeField] private Color targetColor = Color.cyan;
        
        private Mesh mesh;
        private Color[] colors;

        void Start()
        {
            mesh = GetComponent<MeshFilter>().mesh;
            colors = new Color[mesh.vertexCount];
        }

        void Update()
        {
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = targetColor;
            }

            mesh.colors = colors;
        }
    }
}