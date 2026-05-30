using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace VectoringTargetHUD_Engine
{
    internal sealed class TargetHudLineController : MonoBehaviour
    {
        private struct RenderFrame
        {
            public bool Valid;
            public bool HoldMode;
            public Vector2 StartScreen;
            public Vector2 DeltaScreen;
            public float Length;
            public float AngleDeg;
            public string DebugLabel;
        }

        private FlightHud _flightHud;
        private Image _lineImage;
        private Text _debugText;
        private Camera _cam;
        private RectTransform _hudRoot;

        private Unit _activeTarget;
        private Unit _lastTarget;
        private Unit _candidateTarget;
        private float _lastSeenTime;
        private float _nextTick;
        private Vector2 _smoothedDirectionScreen;
        private bool _usingHold;
        private Vector3 _holdWorldPosition;
        private Vector3 _holdNoseDirection;
        private float _holdSpeedMps;
        private float _candidateSeenTime;
        private float _markerFadeStartTime;

        private const float MinAirTargetSpeedKmh = 60f;
        private const float KmhToMps = 1f / 3.6f;
        private const float MaxMarkerRangeMeters = 80000f;
        private const float MarkerAppearDelaySeconds = 1f;
        private const float MarkerBlinkSettleSeconds = 1f;
        private const float MarkerBlinkFrequencyHz = 7f;

        private PrismPointerGraphic _prismGraphic;
        private Vector2 _smoothedPrismSize;
        private readonly List<Graphic> _hudGraphicsCache = new List<Graphic>(64);
        private readonly Dictionary<int, float> _hudColorBuckets = new Dictionary<int, float>(64);
        private Color _cachedHudLiveColor;
        private float _nextHudColorSampleTime;

        private void Start()
        {
            _flightHud = GetComponent<FlightHud>();
            _cam = SceneSingleton<CameraStateManager>.i != null ? SceneSingleton<CameraStateManager>.i.mainCamera : Camera.main;
            BuildHudUi();
        }

        private bool _usePrismCached;
        private int _configFrame = -1;
        private float _updateRateHz;
        private float _positionSmoothing;
        private float _maxScreenStepPx;
        private float _holdWindowSeconds;
        private float _switchHysteresisPx;
        private float _lineThicknessPx;
        private float _perspectiveThicknessBoost;
        private float _prismMinLengthPx;
        private float _prismBaseWidthPx;
        private float _prismTipWidthPx;
        private float _prismDepthSkew;
        private float _prismAlphaGradient;
        private float _prismBaseOffsetPx;
        private float _noseDotDistanceMeters;
        private float _noseDotDistanceByRangeFactor;
        private float _noseDotDistanceMaxMeters;
        private float _noseDotDistanceMinMeters;
        private float _nearDistanceMeters;
        private float _nearDistanceScale;
        private bool _debugMode;
        private float _lineAlpha;
        private Color _liveColorBase;
        private Color _holdColorBase;

        private void EnsureRenderConfig()
        {
            int frame = Time.frameCount;
            if (frame == _configFrame)
                return;
            _configFrame = frame;
            _updateRateHz = Mathf.Max(1f, GetValue(VectoringTargetHUDPlugin.UpdateRateHz, 20f));
            _positionSmoothing = Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.PositionSmoothing, 0.2f));
            _maxScreenStepPx = Mathf.Max(10f, GetValue(VectoringTargetHUDPlugin.MaxScreenStepPx, 260f));
            _holdWindowSeconds = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.HoldWindowSeconds, 1f), 0f, 3f);
            _switchHysteresisPx = Mathf.Max(0f, GetValue(VectoringTargetHUDPlugin.SwitchHysteresisPx, 28f));
            _lineThicknessPx = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.LineThicknessPx, 2.5f), 1f, 12f);
            _perspectiveThicknessBoost = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.PerspectiveThicknessBoost, 0.4f), 0f, 2f);
            _prismMinLengthPx = Mathf.Max(1f, GetValue(VectoringTargetHUDPlugin.PrismMinLengthPx, 10f));
            _prismBaseWidthPx = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.PrismBaseWidthPx, 18f), 4f, 80f);
            _prismTipWidthPx = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.PrismTipWidthPx, 2.5f), 1f, _prismBaseWidthPx);
            _prismDepthSkew = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.PrismDepthSkew, 0.35f), 0f, 1f);
            _prismAlphaGradient = Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.PrismAlphaGradient, 0.45f));
            _prismBaseOffsetPx = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.PrismBaseOffsetPx, 8f), 0f, 50f);
            _noseDotDistanceMeters = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.NoseDotDistanceMeters, 80f), 8f, 500f);
            _noseDotDistanceByRangeFactor = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.NoseDotDistanceByRangeFactor, 0.05f), 0f, 1f);
            _noseDotDistanceMaxMeters = Mathf.Max(_noseDotDistanceMeters, GetValue(VectoringTargetHUDPlugin.NoseDotDistanceMaxMeters, 1200f));
            _noseDotDistanceMinMeters = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.NoseDotDistanceMinMeters, 20f), 4f, _noseDotDistanceMaxMeters);
            _nearDistanceMeters = Mathf.Max(1f, GetValue(VectoringTargetHUDPlugin.NearDistanceMeters, 1200f));
            _nearDistanceScale = Mathf.Clamp(GetValue(VectoringTargetHUDPlugin.NearDistanceScale, 0.55f), 0.1f, 1f);
            _debugMode = GetValue(VectoringTargetHUDPlugin.DebugMode, false);
            _usePrismCached = string.Equals(
                GetString(VectoringTargetHUDPlugin.ShapeMode, "Prism") ?? "Prism",
                "Prism",
                System.StringComparison.OrdinalIgnoreCase);
            _lineAlpha = Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.LineAlpha, 0.9f));
            _liveColorBase = new Color(
                Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.LiveColorR, 0.2f)),
                Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.LiveColorG, 1f)),
                Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.LiveColorB, 0.35f)),
                _lineAlpha);
            _holdColorBase = new Color(
                Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.HoldColorR, 1f)),
                Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.HoldColorG, 0.85f)),
                Mathf.Clamp01(GetValue(VectoringTargetHUDPlugin.HoldColorB, 0.2f)),
                _lineAlpha);
        }

        private void Update()
        {
            if (_lineImage == null || _flightHud == null)
            {
                return;
            }

            EnsureRenderConfig();

            if (_cam == null)
            {
                _cam = SceneSingleton<CameraStateManager>.i != null ? SceneSingleton<CameraStateManager>.i.mainCamera : Camera.main;
            }

            if (Time.unscaledTime >= _nextTick)
            {
                _nextTick = Time.unscaledTime + 1f / _updateRateHz;
                UpdateActiveTarget();
            }

            DrawLineFrame();
        }

        private void OnDestroy()
        {
            if (_lineImage != null)
            {
                Destroy(_lineImage.gameObject);
            }
            if (_debugText != null)
            {
                Destroy(_debugText.gameObject);
            }
        }

        private void BuildHudUi()
        {
            Transform center = _flightHud.GetHUDCenter();
            if (center == null)
            {
                return;
            }

            _hudRoot = center as RectTransform;

            var lineObj = new GameObject("VectoringTargetLine");
            lineObj.transform.SetParent(center, false);
            _lineImage = lineObj.AddComponent<Image>();
            _lineImage.raycastTarget = false;
            _lineImage.color = GetLiveColor();
            _lineImage.enabled = false;
            _lineImage.rectTransform.pivot = new Vector2(0f, 0.5f);
            _lineImage.rectTransform.anchorMin = new Vector2(0f, 0f);
            _lineImage.rectTransform.anchorMax = new Vector2(0f, 0f);

            var prismRoot = new GameObject("VectoringTargetPrism");
            prismRoot.transform.SetParent(center, false);
            _prismGraphic = prismRoot.AddComponent<PrismPointerGraphic>();
            _prismGraphic.raycastTarget = false;
            SetPrismVisible(false);

            var debugObj = new GameObject("VectoringTargetDebug");
            debugObj.transform.SetParent(center, false);
            _debugText = debugObj.AddComponent<Text>();
            _debugText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _debugText.fontSize = 12;
            _debugText.color = new Color(0.9f, 1f, 0.9f, 1f);
            _debugText.alignment = TextAnchor.UpperCenter;
            _debugText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _debugText.verticalOverflow = VerticalWrapMode.Overflow;
            _debugText.enabled = false;
        }

        private void UpdateActiveTarget()
        {
            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null || combatHud.aircraft == null)
            {
                _activeTarget = null;
                return;
            }

            var targets = combatHud.GetTargetList();
            Unit candidate = SelectClosestVisibleToHudCenter(targets);
            if (candidate != null)
            {
                if (_candidateTarget != candidate)
                {
                    _candidateTarget = candidate;
                    _candidateSeenTime = Time.timeSinceLevelLoad;
                }

                float candidateAge = Time.timeSinceLevelLoad - _candidateSeenTime;
                if (candidateAge < MarkerAppearDelaySeconds)
                {
                    _activeTarget = null;
                    _usingHold = false;
                    return;
                }

                if (_activeTarget != null && _activeTarget != candidate && IsObservedNow(_activeTarget))
                {
                    float currentScore = ScreenScore(_activeTarget);
                    float newScore = ScreenScore(candidate);
                    float hysteresis = _switchHysteresisPx;
                    if (newScore + hysteresis >= currentScore)
                    {
                        candidate = _activeTarget;
                    }
                }

                bool targetChanged = _activeTarget != candidate;
                _activeTarget = candidate;
                _lastTarget = candidate;
                _lastSeenTime = Time.timeSinceLevelLoad;
                _holdWorldPosition = candidate.transform.position;
                _holdNoseDirection = candidate.transform.forward;
                _holdSpeedMps = Mathf.Max(0f, candidate.speed);
                _usingHold = false;
                if (targetChanged)
                {
                    _markerFadeStartTime = Time.timeSinceLevelLoad;
                }
                return;
            }

            _candidateTarget = null;
            _candidateSeenTime = 0f;
            float hold = _holdWindowSeconds;
            if (_lastTarget != null && Time.timeSinceLevelLoad - _lastSeenTime <= hold && IsAirTarget(_lastTarget) && _holdSpeedMps >= MinAirTargetSpeedKmh * KmhToMps)
            {
                _activeTarget = _lastTarget;
                _usingHold = true;
                return;
            }

            _activeTarget = null;
            _usingHold = false;
            _markerFadeStartTime = 0f;
        }

        private Unit SelectClosestVisibleToHudCenter(List<Unit> targets)
        {
            if (targets == null || targets.Count == 0 || _cam == null)
            {
                return null;
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Unit best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                Unit unit = targets[i];
                if (unit == null || unit.disabled)
                {
                    continue;
                }
                if (!IsMarkerEligibleLive(unit))
                {
                    continue;
                }

                Vector3 sp = _cam.WorldToScreenPoint(unit.transform.position);
                if (sp.z <= 0f)
                {
                    continue;
                }

                float score = Vector2.Distance(center, new Vector2(sp.x, sp.y));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = unit;
                }
            }

            return best;
        }

        private float ScreenScore(Unit unit)
        {
            if (unit == null || _cam == null)
            {
                return float.MaxValue;
            }

            Vector3 sp = _cam.WorldToScreenPoint(unit.transform.position);
            if (sp.z <= 0f)
            {
                return float.MaxValue;
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return Vector2.Distance(center, new Vector2(sp.x, sp.y));
        }

        private void DrawLineFrame()
        {
            var frame = BuildRenderFrame();
            if (!frame.Valid)
            {
                HideLine();
                return;
            }

            if (UsePrismMode())
            {
                DrawPrism(frame);
            }
            else
            {
                DrawLegacyLine(frame);
            }

            DrawDebug(frame);
        }

        private RenderFrame BuildRenderFrame()
        {
            var frame = new RenderFrame
            {
                Valid = false
            };
            if (_activeTarget == null || _cam == null)
            {
                return frame;
            }

            Vector3 worldStart = _usingHold ? _holdWorldPosition : _activeTarget.transform.position;
            float targetRangeMeters = Vector3.Distance(_cam.transform.position, worldStart);
            if (targetRangeMeters > MaxMarkerRangeMeters)
            {
                return frame;
            }
            Vector3 startScreen3 = _cam.WorldToScreenPoint(worldStart);
            if (startScreen3.z <= 0f)
            {
                return frame;
            }

            Vector3 movementDirection = _usingHold ? _holdNoseDirection : ResolveTargetMovementDirection();
            if (movementDirection.sqrMagnitude < 0.0001f)
            {
                return frame;
            }

            if (!_usingHold)
            {
                _holdWorldPosition = worldStart;
                _holdNoseDirection = movementDirection;
            }

            float noseDotDistance = ComputeNoseDotDistance(worldStart);
            Vector3 worldEnd = worldStart + movementDirection.normalized * noseDotDistance;
            Vector3 endScreen3 = _cam.WorldToScreenPoint(worldEnd);
            Vector2 startScreen = new Vector2(startScreen3.x, startScreen3.y);
            Vector2 rawDelta = new Vector2(endScreen3.x - startScreen3.x, endScreen3.y - startScreen3.y);
            if (endScreen3.z <= 0f || rawDelta.sqrMagnitude < 0.0001f)
            {
                Vector3 dirNorm = movementDirection.normalized;
                rawDelta = new Vector2(
                    Vector3.Dot(dirNorm, _cam.transform.right),
                    Vector3.Dot(dirNorm, _cam.transform.up));
                if (rawDelta.sqrMagnitude < 0.0001f)
                {
                    // Preserve previous stable heading instead of forcing arbitrary up/down.
                    rawDelta = _smoothedDirectionScreen.sqrMagnitude > 0.0001f ? _smoothedDirectionScreen.normalized : Vector2.right;
                }
                rawDelta = rawDelta.normalized * 45f;
            }

            float smooth = _positionSmoothing;
            if (_smoothedDirectionScreen == Vector2.zero)
            {
                _smoothedDirectionScreen = rawDelta;
            }
            Vector2 lerpedDir = Vector2.Lerp(_smoothedDirectionScreen, rawDelta, 1f - smooth);
            float maxStep = _maxScreenStepPx * Mathf.Max(Time.deltaTime, 0.005f);
            _smoothedDirectionScreen = Vector2.MoveTowards(_smoothedDirectionScreen, lerpedDir, maxStep);

            if (_smoothedDirectionScreen.sqrMagnitude < 0.0001f)
            {
                return frame;
            }
            float length = _smoothedDirectionScreen.magnitude;
            float minVisibleLength = _prismMinLengthPx;
            if (length < minVisibleLength)
            {
                // Keep marker visible at long range by enforcing a minimum on-screen length.
                Vector2 forced = _smoothedDirectionScreen.normalized * minVisibleLength;
                _smoothedDirectionScreen = forced;
                length = minVisibleLength;
            }

            frame.Valid = true;
            frame.HoldMode = _usingHold;
            frame.StartScreen = startScreen;
            frame.DeltaScreen = _smoothedDirectionScreen;
            frame.Length = length;
            frame.AngleDeg = Mathf.Atan2(_smoothedDirectionScreen.y, _smoothedDirectionScreen.x) * Mathf.Rad2Deg;
            frame.DebugLabel = _activeTarget.unitName;
            return frame;
        }

        private float ComputeNoseDotDistance(Vector3 worldStart)
        {
            float baseNoseDotDistance = _noseDotDistanceMeters;
            float rangeFactor = _noseDotDistanceByRangeFactor;
            float maxNoseDotDistance = _noseDotDistanceMaxMeters;
            float minNoseDotDistance = _noseDotDistanceMinMeters;
            float nearDistance = _nearDistanceMeters;
            float nearScale = _nearDistanceScale;
            float targetRange = Vector3.Distance(_cam.transform.position, worldStart);
            float noseDotDistance = baseNoseDotDistance + targetRange * rangeFactor;
            float nearT = Mathf.Clamp01(targetRange / nearDistance);
            float nearMultiplier = Mathf.Lerp(nearScale, 1f, nearT);
            noseDotDistance *= nearMultiplier;

            // Extra long-range amplification to keep direction marker readable up to max range.
            float farBoostT = Mathf.Clamp01((targetRange - 20000f) / 60000f); // 20km..80km
            float farBoost = Mathf.Lerp(1f, 1.7f, farBoostT);
            noseDotDistance *= farBoost;

            return Mathf.Clamp(noseDotDistance, minNoseDotDistance, maxNoseDotDistance);
        }

        private bool TryTrackingFallback(out Vector3 fallbackWorld)
        {
            fallbackWorld = Vector3.zero;
            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null || combatHud.aircraft == null || _activeTarget == null || combatHud.aircraft.NetworkHQ == null)
            {
                return false;
            }

            var tracking = combatHud.aircraft.NetworkHQ.GetTrackingData(_activeTarget.persistentID);
            if (tracking == null)
            {
                return false;
            }

            float hold = _holdWindowSeconds;
            float age = Time.timeSinceLevelLoad - tracking.lastSpottedTime;
            if (age <= hold)
            {
                fallbackWorld = tracking.GetPosition().ToLocalPosition();
                _usingHold = true;
                return true;
            }

            _activeTarget = null;
            _usingHold = false;
            return false;
        }

        private bool IsObservedNow(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null || combatHud.aircraft == null || combatHud.aircraft.NetworkHQ == null)
            {
                return false;
            }

            var tracking = combatHud.aircraft.NetworkHQ.GetTrackingData(unit.persistentID);
            return tracking != null && tracking.Observed();
        }

        private bool IsMarkerEligibleLive(Unit unit)
        {
            if (!IsAirTarget(unit))
            {
                return false;
            }

            if (!IsObservedNow(unit))
            {
                return false;
            }

            return unit.speed >= MinAirTargetSpeedKmh * KmhToMps;
        }

        private static bool IsAirTarget(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            // In this game version aircraft/helicopter/tiltrotor targets are represented as Aircraft.
            if (unit is Aircraft)
            {
                return true;
            }

            // Defensive fallback when runtime type proxies are involved.
            return unit.definition is AircraftDefinition;
        }


        private void HideLine()
        {
            _lineImage.enabled = false;
            SetPrismVisible(false);
            _debugText.enabled = false;
        }

        private Vector3 ResolveTargetMovementDirection()
        {
            if (_activeTarget == null)
            {
                return Vector3.zero;
            }

            if (_activeTarget.rb != null && _activeTarget.rb.velocity.sqrMagnitude > 0.5f)
            {
                return _activeTarget.rb.velocity.normalized;
            }

            if (_activeTarget.speed > 0.5f)
            {
                return _activeTarget.transform.forward;
            }

            return Vector3.zero;
        }

        private float ResolveVisualThickness(Vector3 direction)
        {
            float baseThickness = _lineThicknessPx;
            if (_cam == null || direction.sqrMagnitude < 0.0001f)
            {
                return baseThickness;
            }

            Vector3 dirNorm = direction.normalized;
            float depthComponent = Mathf.Abs(Vector3.Dot(dirNorm, _cam.transform.forward));
            float boost = _perspectiveThicknessBoost;
            return Mathf.Clamp(baseThickness * (1f + depthComponent * boost), 1f, 14f);
        }

        private void DrawLegacyLine(RenderFrame frame)
        {
            SetPrismVisible(false);
            _lineImage.enabled = true;
            var rt = _lineImage.rectTransform;
            rt.sizeDelta = new Vector2(frame.Length, _lineThicknessPx);
            rt.position = new Vector3(frame.StartScreen.x, frame.StartScreen.y, 0f);
            rt.rotation = Quaternion.Euler(0f, 0f, frame.AngleDeg);
            _lineImage.color = frame.HoldMode ? GetHoldColor() : GetLiveColor();
        }

        private void DrawPrism(RenderFrame frame)
        {
            _lineImage.enabled = false;
            SetPrismVisible(true);

            float baseWidth = _prismBaseWidthPx;
            float tipWidth = _prismTipWidthPx;
            float depthSkew = _prismDepthSkew;
            float gradient = _prismAlphaGradient;

            // Smooth width/depth to avoid flicker on aggressive maneuvers.
            Vector2 targetSize = new Vector2(baseWidth, tipWidth);
            if (_smoothedPrismSize == Vector2.zero)
            {
                _smoothedPrismSize = targetSize;
            }
            _smoothedPrismSize = Vector2.Lerp(_smoothedPrismSize, targetSize, 0.25f);

            float width = _smoothedPrismSize.x;
            float tip = _smoothedPrismSize.y;
            float len = frame.Length;
            float baseOffset = _prismBaseOffsetPx;
            // Keep offset safe for short vectors so near/far scaling does not break visibility.
            baseOffset = Mathf.Min(baseOffset, Mathf.Max(0f, len * 0.35f));

            Color root = frame.HoldMode ? GetHoldColor() : GetLiveColor();
            if (_prismGraphic != null)
            {
                var rt = _prismGraphic.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0f, 0.5f);
                Vector2 forward = frame.DeltaScreen.sqrMagnitude > 0.0001f ? frame.DeltaScreen.normalized : Vector2.right;
                Vector2 shiftedStart = frame.StartScreen + forward * baseOffset;
                rt.position = new Vector3(shiftedStart.x, shiftedStart.y, 0f);
                rt.rotation = Quaternion.Euler(0f, 0f, frame.AngleDeg);
                _prismGraphic.SetGeometry(len, width, tip, depthSkew, gradient, root);
            }
        }

        private void SetPrismVisible(bool visible)
        {
            if (_prismGraphic != null) _prismGraphic.enabled = visible;
        }

        private void DrawDebug(RenderFrame frame)
        {
            bool debug = _debugMode;
            _debugText.enabled = debug;
            if (!debug)
            {
                return;
            }

            float hold = _holdWindowSeconds;
            float age = Mathf.Max(0f, Time.timeSinceLevelLoad - _lastSeenTime);
            string mode = frame.HoldMode ? "hold" : "live";
            float confidence = frame.HoldMode ? Mathf.Clamp01(1f - age / Mathf.Max(hold, 0.01f)) : 1f;
            _debugText.text = frame.DebugLabel + " | " + mode + " | age:" + age.ToString("F2") + " | conf:" + confidence.ToString("P0");
            _debugText.rectTransform.position = new Vector3(frame.StartScreen.x, frame.StartScreen.y - 20f, 0f);
        }

        private bool UsePrismMode() => _usePrismCached;

        private Color GetLiveColor()
        {
            Color live = ResolveHudThemeColor(_liveColorBase);
            live.a *= GetAppearanceAlphaMultiplier();
            return live;
        }

        private Color GetHoldColor()
        {
            Color hold = _holdColorBase;
            hold.a *= GetAppearanceAlphaMultiplier();
            return hold;
        }

        private static float GetValue(ConfigEntry<float> entry, float fallback)
        {
            return entry != null ? entry.Value : fallback;
        }

        private static bool GetValue(ConfigEntry<bool> entry, bool fallback)
        {
            return entry != null ? entry.Value : fallback;
        }

        private static string GetString(ConfigEntry<string> entry, string fallback)
        {
            return entry != null ? entry.Value : fallback;
        }

        private Color ResolveHudThemeColor(Color fallback)
        {
            if (_hudRoot == null)
            {
                return fallback;
            }

            if (Time.unscaledTime < _nextHudColorSampleTime && _cachedHudLiveColor.a > 0f)
            {
                return _cachedHudLiveColor;
            }

            _nextHudColorSampleTime = Time.unscaledTime + 0.5f;
            Color best = fallback;
            float bestScore = -1f;

            _hudRoot.GetComponentsInChildren(true, _hudGraphicsCache);
            _hudColorBuckets.Clear();
            for (int i = 0; i < _hudGraphicsCache.Count; i++)
            {
                Graphic graphic = _hudGraphicsCache[i];
                if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (graphic == _lineImage || graphic == _prismGraphic || graphic == _debugText)
                {
                    continue;
                }

                Color c = graphic.color;
                if (c.a < 0.15f)
                {
                    continue;
                }

                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                float saturation = max - min;
                if (max < 0.2f || saturation < 0.04f)
                {
                    continue;
                }

                // Quantize to cluster "same HUD color" tones and pick dominant bucket.
                int qr = Mathf.Clamp(Mathf.RoundToInt(c.r * 15f), 0, 15);
                int qg = Mathf.Clamp(Mathf.RoundToInt(c.g * 15f), 0, 15);
                int qb = Mathf.Clamp(Mathf.RoundToInt(c.b * 15f), 0, 15);
                int key = (qr << 8) | (qg << 4) | qb;

                float weight = c.a * (0.5f + saturation + max * 0.25f);
                if (_hudColorBuckets.TryGetValue(key, out float current))
                {
                    _hudColorBuckets[key] = current + weight;
                }
                else
                {
                    _hudColorBuckets[key] = weight;
                }
            }

            foreach (var kv in _hudColorBuckets)
            {
                if (kv.Value <= bestScore)
                {
                    continue;
                }

                int key = kv.Key;
                float r = ((key >> 8) & 0xF) / 15f;
                float g = ((key >> 4) & 0xF) / 15f;
                float b = (key & 0xF) / 15f;
                bestScore = kv.Value;
                best = new Color(r, g, b, 1f);
            }

            best.a = _lineAlpha;
            _cachedHudLiveColor = best;
            return _cachedHudLiveColor;
        }

        private float GetAppearanceAlphaMultiplier()
        {
            if (_markerFadeStartTime <= 0f)
            {
                return 1f;
            }

            float t = Mathf.Max(0f, Time.timeSinceLevelLoad - _markerFadeStartTime);
            if (t >= MarkerBlinkSettleSeconds)
            {
                return 1f;
            }

            float envelope = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, MarkerBlinkSettleSeconds));
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * MarkerBlinkFrequencyHz * Mathf.PI * 2f);
            float blinkAlpha = Mathf.Lerp(0.3f, 1f, pulse);
            return Mathf.Lerp(1f, blinkAlpha, envelope);
        }
    }
}
