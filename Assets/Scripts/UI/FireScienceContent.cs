using System.Collections.Generic;
using UnityEngine;

/// <summary>火灾科普内容资产：分类图文卡片 + 自测问答。内容与表现分离，全部文案在本资产中配置。</summary>
[CreateAssetMenu(menuName = "Fire Game/FireScience Content", fileName = "FireScience_Content")]
public class FireScienceContent : ScriptableObject
{
    [System.Serializable]
    public class ScienceCard
    {
        [TextArea(1, 2)] public string heading;
        [TextArea(4, 10)] public string body;
        [TextArea(1, 2)] public string practice;     // "在XX关中实践…"，可空
        public Sprite illustration;                   // 可空
    }

    [System.Serializable]
    public class ScienceCategory
    {
        public string title;
        [TextArea(1, 2)] public string intro;
        public List<ScienceCard> cards = new List<ScienceCard>();
    }

    [System.Serializable]
    public class QuizItem      // 与 QuizDoor.QuizQuestion 同构，多一配图字段
    {
        [TextArea(2, 3)] public string question;
        [TextArea(1, 2)] public string optionA;
        [TextArea(1, 2)] public string optionB;
        [TextArea(1, 2)] public string optionC;
        public int correctIndex;                      // 0=A 1=B 2=C
        [TextArea(1, 3)] public string explanation;
        public Sprite illustration;                   // 题目配图，可空
    }

    public List<ScienceCategory> categories = new List<ScienceCategory>();
    public List<QuizItem> quizItems = new List<QuizItem>();
}
