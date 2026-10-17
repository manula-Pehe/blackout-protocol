using UnityEngine;
using UnityEngine.UI;

namespace BlackoutProtocol.Player
{
    /// <summary>
    /// Builds the player's compact runtime HUD and reflects health and ammunition state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerWeapon))]
    public sealed class PlayerCombatHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.025f, 0.055f, 0.075f, 0.88f);
        private static readonly Color AccentColor = new Color(0.12f, 0.82f, 0.94f, 1f);
        private static readonly Color LabelColor = new Color(0.06f, 0.62f, 0.7f, 1f);
        private static readonly Color HealthColor = new Color(0.22f, 0.86f, 0.68f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.62f, 0.2f, 1f);
        private static readonly Color CrosshairColor = new Color(0.025f, 0.3f, 0.36f, 1f);

        private PlayerHealth playerHealth;
        private PlayerWeapon playerWeapon;
        private Texture2D whiteTexture;
        private Sprite whiteSprite;
        private Image healthFill;
        private Text healthText;
        private Text healthValueText;
        private Text ammoText;
        private Text reserveAmmoText;
        private Text magazineCaptionText;
        private Text reserveCaptionText;
        private Text reloadPromptText;
        private GameObject reloadPromptPanelObject;
        private RectTransform reloadPromptPanelRect;
        private GameObject hudCanvasObject;

        private void Awake()
        {
            playerHealth = GetComponent<PlayerHealth>();
            playerWeapon = GetComponent<PlayerWeapon>();
            BuildHud();
            RefreshHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            RefreshAmmo(playerWeapon.CurrentAmmo, playerWeapon.ReserveAmmo, playerWeapon.MagazineSize);
        }

        private void OnEnable()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (playerWeapon == null)
            {
                playerWeapon = GetComponent<PlayerWeapon>();
            }

            playerHealth.HealthChanged += RefreshHealth;
            playerWeapon.AmmoChanged += RefreshAmmo;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= RefreshHealth;
            }

            if (playerWeapon != null)
            {
                playerWeapon.AmmoChanged -= RefreshAmmo;
            }
        }

        private void OnDestroy()
        {
            if (hudCanvasObject != null)
            {
                Destroy(hudCanvasObject);
            }

            if (whiteSprite != null)
            {
                Destroy(whiteSprite);
            }

            if (whiteTexture != null)
            {
                Destroy(whiteTexture);
            }
        }

        private void Update()
        {
            if (ammoText == null || playerWeapon == null)
            {
                return;
            }

            ammoText.color = playerWeapon.IsReloading || playerWeapon.CurrentAmmo <= 2
                ? WarningColor
                : Color.white;

            RefreshReloadPrompt();
        }

        private void BuildHud()
        {
            hudCanvasObject = new GameObject("Player Combat HUD");
            GameObject canvasObject = hudCanvasObject;

            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Player HUD White Pixel",
                hideFlags = HideFlags.HideAndDontSave
            };
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f));
            whiteSprite.name = "Player HUD White Sprite";
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform healthPanel = CreateRect("Health Panel", canvasObject.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(330f, 86f), new Vector2(34f, -34f));
            AddImage(healthPanel.gameObject, PanelColor);

            RectTransform statusAccent = CreateRect("Status Accent", healthPanel,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(3f, 52f), new Vector2(10f, 0f));
            AddImage(statusAccent.gameObject, AccentColor);

            healthText = CreateText("Health Label", healthPanel, font, 18,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0.58f, 1f),
                new Vector2(19f, -20f), new Vector2(-18f, 32f));
            healthText.text = "HEALTH";
            healthText.color = LabelColor;

            healthValueText = CreateText("Health Value", healthPanel, font, 18,
                TextAnchor.MiddleRight, new Vector2(0.58f, 1f), new Vector2(1f, 1f),
                new Vector2(-14f, -20f), new Vector2(-8f, 32f));

            RectTransform healthTrack = CreateRect("Health Track", healthPanel,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-32f, 16f), new Vector2(0f, 14f));
            AddImage(healthTrack.gameObject, new Color(0.08f, 0.14f, 0.17f, 1f));

            RectTransform fillRect = CreateRect("Health Fill", healthTrack,
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero);
            healthFill = AddImage(fillRect.gameObject, HealthColor);
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            healthFill.raycastTarget = false;

            for (int segment = 1; segment < 8; segment++)
            {
                float normalizedPosition = segment / 8f;
                RectTransform divider = CreateRect($"Health Segment {segment}", healthTrack,
                    new Vector2(normalizedPosition, 0f),
                    new Vector2(normalizedPosition, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(3f, -4f), Vector2.zero);
                AddImage(divider.gameObject, PanelColor);
            }

            RectTransform ammoPanel = CreateRect("Ammo Panel", canvasObject.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(320f, 150f), new Vector2(-34f, 34f));
            AddImage(ammoPanel.gameObject, PanelColor);

            ammoText = CreateText("Magazine Count", ammoPanel, font, 36,
                TextAnchor.MiddleCenter, new Vector2(0f, 0.38f), new Vector2(0.5f, 0.88f),
                Vector2.zero, new Vector2(-20f, -4f));
            ammoText.fontStyle = FontStyle.Bold;
            reserveAmmoText = CreateText("Reserve Count", ammoPanel, font, 36,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.38f), new Vector2(1f, 0.88f),
                Vector2.zero, new Vector2(-20f, -4f));
            reserveAmmoText.fontStyle = FontStyle.Bold;

            magazineCaptionText = CreateText("Magazine Caption", ammoPanel, font, 16,
                TextAnchor.MiddleCenter, new Vector2(0f, 0.2f), new Vector2(0.5f, 0.38f),
                Vector2.zero, new Vector2(-20f, -2f));
            magazineCaptionText.text = "MAGAZINE";
            magazineCaptionText.color = LabelColor;
            magazineCaptionText.fontStyle = FontStyle.Bold;
            reserveCaptionText = CreateText("Reserve Caption", ammoPanel, font, 16,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.2f), new Vector2(1f, 0.38f),
                Vector2.zero, new Vector2(-20f, -2f));
            reserveCaptionText.text = "RESERVE";
            reserveCaptionText.color = LabelColor;
            reserveCaptionText.fontStyle = FontStyle.Bold;

            RectTransform ammoDivider = CreateRect("Ammo Divider", ammoPanel,
                new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.82f),
                new Vector2(0.5f, 0.5f), new Vector2(2f, -4f), Vector2.zero);
            AddImage(ammoDivider.gameObject, new Color(AccentColor.r, AccentColor.g,
                AccentColor.b, 0.45f));

            RectTransform reloadPromptPanel = CreateRect("Reload Prompt Panel", canvasObject.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(380f, 68f), new Vector2(0f, -70f));
            AddImage(reloadPromptPanel.gameObject, new Color(PanelColor.r, PanelColor.g,
                PanelColor.b, 0.96f));
            reloadPromptPanelObject = reloadPromptPanel.gameObject;
            reloadPromptPanelRect = reloadPromptPanel;

            reloadPromptText = CreateText("Reload Prompt", reloadPromptPanel, font, 26,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-28f, -8f));
            reloadPromptText.fontStyle = FontStyle.Bold;
            reloadPromptPanelObject.SetActive(false);

            CreateCrosshair(canvasObject.transform);
        }

        private void CreateCrosshair(Transform parent)
        {
            AddCrosshairPiece(parent, "Crosshair Top", new Vector2(4f, 16f), new Vector2(0f, 10f));
            AddCrosshairPiece(parent, "Crosshair Bottom", new Vector2(4f, 16f), new Vector2(0f, -10f));
            AddCrosshairPiece(parent, "Crosshair Left", new Vector2(16f, 4f), new Vector2(-10f, 0f));
            AddCrosshairPiece(parent, "Crosshair Right", new Vector2(16f, 4f), new Vector2(10f, 0f));
            AddCrosshairPiece(parent, "Crosshair Center", new Vector2(3f, 3f), Vector2.zero);
        }

        private void AddCrosshairPiece(Transform parent, string objectName,
            Vector2 size, Vector2 position)
        {
            RectTransform rect = CreateRect(objectName, parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position);
            AddImage(rect.gameObject, CrosshairColor).raycastTarget = false;
        }

        private void RefreshHealth(float current, float maximum)
        {
            if (healthFill != null)
            {
                healthFill.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
                healthFill.color = current / Mathf.Max(1f, maximum) <= 0.25f
                    ? WarningColor
                    : HealthColor;
            }

            if (healthValueText != null)
            {
                healthValueText.text = $"{Mathf.CeilToInt(current):000} / {Mathf.CeilToInt(maximum):000}";
            }
        }

        private void RefreshAmmo(int current, int reserve, int magazineSize)
        {
            if (ammoText != null && playerWeapon != null && !playerWeapon.IsReloading)
            {
                ammoText.text = $"{current:00}";
                ammoText.color = current <= 2 ? WarningColor : Color.white;
            }

            if (reserveAmmoText != null)
            {
                reserveAmmoText.text = $"{reserve:00}";
            }

            RefreshReloadPrompt();
        }

        private void RefreshReloadPrompt()
        {
            if (reloadPromptText == null || playerWeapon == null)
            {
                return;
            }

            bool magazineEmpty = playerWeapon.CurrentAmmo <= 0;
            bool canReload = playerWeapon.ReserveAmmo > 0;
            bool isReloading = playerWeapon.IsReloading;
            bool showPrompt = isReloading || magazineEmpty;
            if (reloadPromptPanelObject.activeSelf != showPrompt)
            {
                reloadPromptPanelObject.SetActive(showPrompt);
            }

            if (!showPrompt)
            {
                return;
            }

            if (isReloading)
            {
                reloadPromptText.text = "RELOADING";
                reloadPromptText.color = WarningColor;
                reloadPromptPanelRect.localScale = Vector3.one;
            }
            else if (canReload)
            {
                reloadPromptText.text = "PRESS R TO RELOAD";
                float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.6f) + 1f) * 0.5f;
                Color pulsingColor = WarningColor;
                pulsingColor.a = Mathf.Lerp(0.55f, 1f, pulse);
                reloadPromptText.color = pulsingColor;
                float scale = Mathf.Lerp(0.98f, 1.035f, pulse);
                reloadPromptPanelRect.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                reloadPromptText.text = "NO AMMO IN RESERVE";
                reloadPromptText.color = WarningColor;
                reloadPromptPanelRect.localScale = Vector3.one;
            }
        }

        private static RectTransform CreateRect(string objectName, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private Image AddImage(GameObject target, Color color)
        {
            Image image = target.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string objectName, Transform parent, Font font, int fontSize,
            TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax,
                new Vector2(0.5f, 0.5f), size, position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
