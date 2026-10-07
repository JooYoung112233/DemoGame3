using System.Collections.Generic;
using System.Globalization;

namespace Demo6.Core.Save
{
    /// <summary>
    /// 지난 알림 한 줄(기획/저장-처음화면-멈춤창-1차.md 7장, 11-7 'D'). 글, 곳('마을'·'1층'·'굴'·'시험장', 없으면 null), 이어 온 수.
    /// </summary>
    public readonly struct NoticeEntry
    {
        public readonly string Text;
        public readonly string Place;
        public readonly int Count;

        public NoticeEntry(string text, string place, int count)
        {
            Text = text;
            Place = place;
            Count = count;
        }

        /// <summary>화면 한 줄: '[2층] 글 ×3'. 곳이 없으면 '[ ]' 없이 글만, 한 번이면 '×1'을 붙이지 않는다.</summary>
        public string Line()
        {
            string head = string.IsNullOrEmpty(Place) ? "" : "[" + Place + "] ";
            return Count > 1 ? head + Text + " ×" + Count.ToString(CultureInfo.InvariantCulture) : head + Text;
        }
    }

    /// <summary>
    /// 지난 알림 목록(저장·처음 화면·멈춤 창 1차 7장). 화면 아래 알림 한 줄과 레벨업을 Capacity(40)줄까지 담고, 넘으면 가장 오래된 것부터 버린다.
    /// 같은 글이 같은 곳에서 바로 이어 오면 새 줄을 만들지 않고 그 줄의 수를 하나 늘린다(곳이 다르면 따로).
    /// 게임을 켠 동안만 남는다(디스크에 쓰지 않음). 듣기·그리기는 Game의 NoticeLog가 한다. Core라 UnityEngine을 쓰지 않는다.
    /// </summary>
    public sealed class NoticeHistory
    {
        /// <summary>담는 줄 수(7장 '40줄까지').</summary>
        public const int Capacity = 40;

        readonly List<NoticeEntry> _entries = new List<NoticeEntry>(Capacity + 1);

        /// <summary>담긴 줄(오래된 것부터, 맨 끝이 가장 최근).</summary>
        public IReadOnlyList<NoticeEntry> Entries => _entries;

        /// <summary>목록이 바뀔 때마다(새 줄·합침·비움) 하나씩 는다. 그리는 쪽이 '늘었나'를 볼 때 쓴다.</summary>
        public int Revision { get; private set; }

        /// <summary>
        /// 한 줄 더한다. 글은 앞뒤 빈칸을 빼고, 비면 무시한다. 곳도 앞뒤 빈칸을 빼고 비면 '곳 없음'(null)으로 둔다.
        /// 마지막 줄과 글·곳이 같으면 그 줄의 수 +1, 아니면 끝에 더하고 Capacity를 넘으면 가장 오래된 줄부터 뺀다.
        /// </summary>
        public void Add(string text, string place)
        {
            if (text == null) return;
            text = text.Trim();
            if (text.Length == 0) return;
            place = string.IsNullOrWhiteSpace(place) ? null : place.Trim();
            int last = _entries.Count - 1;
            if (last >= 0 && _entries[last].Text == text && _entries[last].Place == place)
            {
                _entries[last] = new NoticeEntry(text, place, _entries[last].Count + 1);
            }
            else
            {
                _entries.Add(new NoticeEntry(text, place, 1));
                while (_entries.Count > Capacity) _entries.RemoveAt(0);
            }
            Revision++;
        }

        /// <summary>모두 비운다(이어하기·새로 시작 때, 7장).</summary>
        public void Clear()
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            Revision++;
        }
    }
}
