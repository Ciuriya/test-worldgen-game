using UnityEngine;

namespace PendingName.WorldGen {
    
    [CreateAssetMenu(menuName = "WorldGen/Graph Grammar Rule")]
    public class GraphGrammarRule : ScriptableObject {

        [Tooltip("The application limit of this rule, how many times it can convert zones into the output room")]
        [Range(0, 1000)] public int ApplicationLimit;

        [Tooltip("The grammar rules that select which zones will be converted to the output")]
        public GrammarInput Input;

        [Tooltip("All zones will be set to this room output")]
        public Room Output;
    }
}