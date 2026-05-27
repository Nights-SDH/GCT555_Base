using UnityEngine;

public enum WIMCommandStyle
{
    Puppet,
    Symbolic
}

public enum WIMInfoDensity
{
    Continuous,
    Discrete
}

public enum WIMExperimentCondition
{
    C1_Puppet_Continuous,
    C2_Symbolic_Continuous,
    C3_Puppet_Discrete,
    C4_Symbolic_Discrete
}

public class ExperimentConditionManager : MonoBehaviour
{
    [Header("Current Experiment Condition")]
    public WIMExperimentCondition condition = WIMExperimentCondition.C1_Puppet_Continuous;

    public WIMCommandStyle CommandStyle
    {
        get
        {
            switch (condition)
            {
                case WIMExperimentCondition.C1_Puppet_Continuous:
                case WIMExperimentCondition.C3_Puppet_Discrete:
                    return WIMCommandStyle.Puppet;

                case WIMExperimentCondition.C2_Symbolic_Continuous:
                case WIMExperimentCondition.C4_Symbolic_Discrete:
                    return WIMCommandStyle.Symbolic;

                default:
                    return WIMCommandStyle.Puppet;
            }
        }
    }

    public WIMInfoDensity InfoDensity
    {
        get
        {
            switch (condition)
            {
                case WIMExperimentCondition.C1_Puppet_Continuous:
                case WIMExperimentCondition.C2_Symbolic_Continuous:
                    return WIMInfoDensity.Continuous;

                case WIMExperimentCondition.C3_Puppet_Discrete:
                case WIMExperimentCondition.C4_Symbolic_Discrete:
                    return WIMInfoDensity.Discrete;

                default:
                    return WIMInfoDensity.Discrete;
            }
        }
    }

    public bool IsPuppet => CommandStyle == WIMCommandStyle.Puppet;
    public bool IsSymbolic => CommandStyle == WIMCommandStyle.Symbolic;
    public bool IsContinuous => InfoDensity == WIMInfoDensity.Continuous;
    public bool IsDiscrete => InfoDensity == WIMInfoDensity.Discrete;
}