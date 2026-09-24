using System.Collections.Generic;

namespace Demo5.FrontEnd
{
    public sealed partial class FieldTurnPlanner
    {
        ExpeditionNpcStory Story => arrival ? arrival.GetComponent<ExpeditionNpcStory>() : null;
        string ObservationLabel(string id)
        {
            var story = Story;
            return story ? story.ObserveLabel(id) : "흔적";
        }
        void CompleteObservations(FieldPlanCheck check)
        {
            var story = Story;
            if (!story) return;
            foreach (var observation in check.Observations)
                story.CompleteObservation(observation.Id, observation.Member);
        }
        string ObservationPreview(FieldPlanCheck check)
        {
            var parts = new List<string>();
            if (check.Runs.Count > 0) parts.Add("수색 " + check.Runs.Count + "곳");
            if (check.Listens.Count > 0) parts.Add("귀 대기 " + check.Listens.Count + "곳");
            parts.Add(string.Format(Texts.ChipObserve, check.Observations.Count));
            return string.Join(" · ", parts);
        }
        string ObservationStatus(FieldPlanCheck check)
        {
            if (check.Observations.Count == 0) return null;
            if (check.Runs.Count == 0 && check.Listens.Count == 0)
                return string.Format(Texts.StatusObserved, check.Observations.Count);
            var parts = new List<string> { "관찰 " + check.Observations.Count + "곳 완료" };
            if (check.Runs.Count > 0) parts.Add("수색 " + check.Runs.Count + "곳 진행");
            if (check.Listens.Count > 0) parts.Add("귀 대기 " + check.Listens.Count + "곳");
            return string.Join(" · ", parts) + "\n수색물과 확인한 단서를 차례로 살펴보세요.";
        }
    }
}
