using System.Collections.Generic;
using UnityEngine;

/// <summary>火灾科普内容资产：分类图文卡片（自测问答题库已迁移至独立的 QuizContent 资产）。内容与表现分离，全部文案在本资产中配置。</summary>
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

    public List<ScienceCategory> categories = new List<ScienceCategory>();
}
