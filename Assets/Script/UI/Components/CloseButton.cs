using UnityEngine;
using UnityEngine.UI;

public class CloseButton : MonoBehaviour {

    [SerializeField] private GameObject targetPanel;

    [SerializeField] private Button btn;

    private void OnEnable() {
        btn ??= GetComponent<Button>();

        btn.onClick.AddListener(ClosePanel);
    }

    private void OnDisable() {
        btn.onClick.RemoveListener(ClosePanel);
    }

    public void ClosePanel() {
        if ( targetPanel != null ) {
            targetPanel.SetActive(false);
        }
    }

}
