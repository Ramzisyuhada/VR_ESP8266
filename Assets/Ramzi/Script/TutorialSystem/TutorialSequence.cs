using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "TutorialSequence", menuName = "Tutorial/Sequence", order = 1)]

public class TutorialSequence : ScriptableObject
{
    public TutorialStep[] Steps;

}
