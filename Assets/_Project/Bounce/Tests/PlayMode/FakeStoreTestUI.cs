using System.Collections;
using System.Linq;
using System.Reflection;
using BK.UI;
using NUnit.Framework;
using UnityEngine;

internal static class FakeStoreTestUI
{
    public static IEnumerator Resolve(bool buy)
    {
        MonoBehaviour dialog=null;
        var until=Time.realtimeSinceStartup+20;
        while(dialog==null && Time.realtimeSinceStartup<until)
        {
            dialog=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .FirstOrDefault(x=>x.GetType().FullName=="UnityEngine.Purchasing.UIFakeStoreWindow");
            yield return null;
        }
        Assert.That(dialog,Is.Not.Null,"Official Fake Store dialog should open.");
        dialog.GetType().GetMethod(buy?"OnOkClicked":"OnCancelClicked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,null);
        MessagePopupView message=null;
        until=Time.realtimeSinceStartup+20;
        while(message==null && Time.realtimeSinceStartup<until)
        { message=Object.FindFirstObjectByType<MessagePopupView>();yield return null; }
        Assert.That(message,Is.Not.Null,"Purchase result should be shown.");
        yield return new WaitForSecondsRealtime(.25f);
        message.OnBackRequested();
        until=Time.realtimeSinceStartup+10;
        while(message!=null && Time.realtimeSinceStartup<until)yield return null;
        Assert.That(message==null,Is.True,"Purchase message must finish closing.");
        yield return new WaitForSecondsRealtime(.35f);
    }
}
