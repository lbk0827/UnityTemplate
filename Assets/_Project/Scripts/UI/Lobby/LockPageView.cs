namespace Project
{
    /// <summary>
    /// GRP1의 panel_lock 페이지에 해당하는 잠금/예정 컨텐츠 화면입니다.
    /// 현재는 표시 전용이며, 실제 컨텐츠가 붙으면 이 뷰에 동작을 추가합니다.
    /// </summary>
    public sealed class LockPageView : ViewComponent
    {
        public void Bind(ProjectViewBase owner) => ClearSubscriptions();
    }
}
