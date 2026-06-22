using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public interface IManagerInterface
{
    IEnumerator Initialize();
    IEnumerator PostInitialize();
    IEnumerator SetForGameplay();
}

public class SceneManagersInitializer : MonoBehaviour
{
    [TitleGroup("Settings"), SerializeField]
    private bool turnOffPanelAfterLoad = true;

    private IEnumerator Start()
    {
        var managers = transform.GetComponentsInChildren<IManagerInterface>();

        SceneLoadManager.Instance.CreateAssetsToLoad(managers.Length * 3, turnOffPanelAfterLoad);


        foreach (var manager in managers)
        {
            yield return StartCoroutine(manager.Initialize());
            SceneLoadManager.Instance.AssetLoaded();
            yield return new WaitForSeconds(.1f);
        }

        yield return new WaitForSeconds(.25f);

        foreach (var manager in managers)
        {
            yield return StartCoroutine(manager.PostInitialize());
            SceneLoadManager.Instance.AssetLoaded();
            yield return new WaitForSeconds(.1f);
        }

        foreach (var manager in managers)
        {
            yield return StartCoroutine(manager.SetForGameplay());
            SceneLoadManager.Instance.AssetLoaded();
            yield return new WaitForSeconds(.1f);
        }

        yield return null;
    }
}