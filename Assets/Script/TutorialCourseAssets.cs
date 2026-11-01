using UnityEngine;

namespace Deadline4Sec
{
    public sealed class TutorialCourseAssets : ScriptableObject
    {
        public GameObject groundEnemy;
        public GameObject airEnemy;
        public GameObject jumpObstacle;
        public GameObject slideObstacle;
        public GameObject laneObstacle;
        public Material floorMaterial;

        [Header("Speaker and swipe guide (Art/Tutorial)")]
        public Sprite portrait;      // tutorial_ex
        public Sprite bubble;        // tutorial_ex_bubble
        public Sprite swipeArrow;    // tutorial_swipe_arrow (pointing up)
        public Sprite swipeHand;     // tutorial_swipe_hand (finger up)
        public TutorialPresentation presentation = new TutorialPresentation();

        [Header("Steps: briefing (InfoOnly) first, then the 10 lessons in course order")]
        public TutorialStep[] steps;

        public bool IsValid => groundEnemy != null && airEnemy != null &&
            jumpObstacle != null && slideObstacle != null && laneObstacle != null && floorMaterial != null;

        // The edited steps when they still describe the ten lessons in course
        // order (any InfoOnly steps before them); otherwise the built-in ones.
        public TutorialStep[] ResolveSteps(int lessonCount)
        {
            TutorialStep[] defaults = TutorialStep.CreateDefaults();
            if (steps == null || steps.Length == 0)
                return defaults;
            int briefing = 0;
            while (briefing < steps.Length && steps[briefing] != null && steps[briefing].kind == TutorialStepKind.InfoOnly)
                briefing++;
            bool valid = steps.Length - briefing == lessonCount;
            int firstDefaultLesson = defaults.Length - lessonCount;
            for (int i = 0; valid && i < lessonCount; i++)
            {
                TutorialStep step = steps[briefing + i];
                valid = step != null && step.kind == TutorialStepKind.WaitForInput &&
                    step.completeCondition == defaults[firstDefaultLesson + i].completeCondition;
            }
            if (valid)
                return steps;
            Debug.LogWarning("TutorialCourse steps do not match the 10 lessons in order; using the built-in steps.");
            return defaults;
        }
    }
}
