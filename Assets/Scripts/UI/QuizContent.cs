using System.Collections.Generic;
using UnityEngine;

/// <summary>消防知识问答题库：与科普内容资产分离，专供独立问答场景随机抽题。scienceCategoryIndex 关联科普页章节，用于错题跳转。</summary>
[CreateAssetMenu(menuName = "Fire Game/Quiz Content", fileName = "Quiz_Content")]
public class QuizContent : ScriptableObject
{
    [System.Serializable]
    public class QuizItem
    {
        [TextArea(2, 3)] public string question;
        [TextArea(1, 2)] public string optionA;
        [TextArea(1, 2)] public string optionB;
        [TextArea(1, 2)] public string optionC;
        public int correctIndex;                      // 0=A 1=B 2=C
        [TextArea(1, 3)] public string explanation;
        public Sprite illustration;                    // 题目配图，可空
        [Range(0, 4)] public int scienceCategoryIndex; // 关联科普章节：0火灾常识 1报警求助 2灭火器使用 3逃生要点 4常见误区
    }

    public List<QuizItem> questions = new List<QuizItem>();
    public int roundSize = 10;                        // 每轮随机抽题数

    private void OnValidate()
    {
        if (roundSize < 1) roundSize = 1;
        if (roundSize > questions.Count) roundSize = Mathf.Max(1, questions.Count);
    }
}
