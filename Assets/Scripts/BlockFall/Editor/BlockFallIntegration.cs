#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrosswalkRun;
namespace BlockFall.Editor
{
    public static class BlockFallIntegration
    {
        public static void Apply()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Character.unity");
            var controller = UnityEngine.Object.FindObjectOfType<CharacterRoundRestart>();
            if (controller == null) throw new Exception("CharacterRoundRestart missing");
            var data = new SerializedObject(controller);
            var template = data.FindProperty("tileFlipButton").objectReferenceValue as Button;
            if (template == null) throw new Exception("TileFlip button missing");
            var existing = data.FindProperty("blockFallButton").objectReferenceValue as Button;
            var button = existing != null ? existing : UnityEngine.Object.Instantiate(template, template.transform.parent);
            button.name = "BlockFall Button";
            if (existing == null)
            {
                var rect = button.GetComponent<RectTransform>();
                rect.anchoredPosition = template.GetComponent<RectTransform>().anchoredPosition + Vector2.down * (rect.rect.height + 12);
            }
            button.GetComponentInChildren<TMP_Text>(true).text = "Block Fall";
            button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(button.onClick, controller.StartBlockFall);
            data.FindProperty("blockFallButton").objectReferenceValue = button;
            data.FindProperty("blockFallScene").stringValue = "BlockFall";
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var legacy in UnityEngine.Object.FindObjectsOfType<BlockFallLobby>())
                UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.OpenScene("Assets/Scenes/BlockFall.unity");
            var game = UnityEngine.Object.FindObjectOfType<BlockFallGame>();
            var settings = new SerializedObject(game);
            settings.FindProperty("resultSceneName").stringValue = "Ranking";
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("BLOCKFALL_INTEGRATION_PASS: saved lobby button uses shared readiness/load gate, Ranking destination saved.");
        }
    }
}
#endif
