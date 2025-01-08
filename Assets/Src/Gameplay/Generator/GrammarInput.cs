using UnityEngine;
using System;
using System.Collections.Generic;
using MyBox;

[Serializable]
public class GrammarInput {

    [Tooltip("True to verify zones randomly, false for sequential")]
    public bool RandomizeVerification;

    [Tooltip("The minimal variation percentage required between changes applied during the last and before last executions of this rule")]
    [Range(0, 100)] public int RuleApplicationMinimalVariationPercentage;

    [Tooltip("All possible rooms for this rule, none = all empty zones are valid")]
    public List<Room> ValidRooms;

    [Tooltip("The distance (in %, 50% = center) between the center of the current zone and the nearest map border")]
    [MinMaxRange(0, 50)] public RangedInt DistanceFromEdge;

    [Tooltip("Number of requirements that must be satisfied for this rule to apply")]
    public int RequirementCount;

    [Tooltip("The requirements to filter the zones to convert with this rule, first checked first served")]
    public List<GraphGrammarRequirement> Requirements;
}
