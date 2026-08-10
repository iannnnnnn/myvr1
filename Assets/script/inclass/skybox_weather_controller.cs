using UnityEngine;
using UnityEngine.Rendering;

public class skybox_weather_controller : MonoBehaviour
{
    /*
        天氣類型
    */
    public enum WeatherType
    {
        Clear,
        Cloudy,
        Storm
    }

    [Header("Skybox")]

    public Material skyboxClearDay;
    public Material skyboxClearNight;

    public Material skyboxCloudyDay;
    public Material skyboxCloudyNight;

    public Material skyboxStormDay;
    public Material skyboxStormNight;


    [Header("Light And Time")]

    public Light directionalLight;

    /*
        每隔多少秒重新決定一次天氣
    */
    public float weatherChangeInterval = 30f;

    /*
        太陽高度小於此數值時視為夜晚

        零代表太陽剛好位於地平線
        稍微設定負值可以避免黃昏時太早切換
    */
    [Range(-0.5f, 0.5f)]
    public float nightThreshold = -0.05f;

    /*
        環境光與霧氣變化速度
    */
    public float environmentTransitionSpeed = 1.5f;


    [Header("Weather Chance")]

    /*
        暴風雨機率
    */
    [Range(0f, 1f)]
    public float stormChance = 0.1f;

    /*
        陰天機率

        實際判斷方式為暴風雨機率加陰天機率
        剩餘機率自動成為晴天
    */
    [Range(0f, 1f)]
    public float cloudyChance = 0.3f;

    /*
        開啟後暴風雨只會在晚上出現
        關閉後白天晚上都可能出現暴風雨
    */
    public bool stormOnlyAtNight = false;


    [Header("Ambient Color")]

    public Color dayAmbient =
        new Color(1f, 1f, 1f);

    public Color nightAmbient =
        new Color(0.1f, 0.1f, 0.2f);

    /*
        陰天環境光會乘上此比例
    */
    [Range(0f, 1f)]
    public float cloudyAmbientMultiplier = 0.7f;

    /*
        暴風雨環境光會乘上此比例
    */
    [Range(0f, 1f)]
    public float stormAmbientMultiplier = 0.4f;


    [Header("Directional Light")]

    public float clearDayLightIntensity = 1f;
    public float cloudyDayLightIntensity = 0.65f;
    public float stormDayLightIntensity = 0.35f;

    public float clearNightLightIntensity = 0.15f;
    public float cloudyNightLightIntensity = 0.1f;
    public float stormNightLightIntensity = 0.05f;


    [Header("Fog Color")]

    public Color dayFog =
        new Color(0.7f, 0.8f, 0.9f);

    public Color nightFog =
        new Color(0.05f, 0.05f, 0.1f);

    public Color cloudyDayFog =
        new Color(0.55f, 0.6f, 0.65f);

    public Color cloudyNightFog =
        new Color(0.08f, 0.09f, 0.12f);

    public Color stormDayFog =
        new Color(0.25f, 0.28f, 0.32f);

    public Color stormNightFog =
        new Color(0.02f, 0.025f, 0.04f);


    [Header("Fog Density")]

    /*
        必須使用 Exponential 或 Exponential Squared 模式
        fogDensity 才會生效
    */
    public FogMode fogMode = FogMode.ExponentialSquared;

    public float clearDayFogDensity = 0.001f;
    public float clearNightFogDensity = 0.002f;

    public float cloudyDayFogDensity = 0.004f;
    public float cloudyNightFogDensity = 0.006f;

    public float stormDayFogDensity = 0.012f;
    public float stormNightFogDensity = 0.018f;


    [Header("Current Status")]

    /*
        顯示目前天氣
        執行時可在 Inspector 查看
    */
    [SerializeField]
    private WeatherType currentWeather = WeatherType.Clear;

    /*
        顯示目前是否為夜晚
    */
    [SerializeField]
    private bool isNight = false;


    /*
        上一幀的日夜狀態
        用來偵測日夜是否剛發生改變
    */
    private bool previousIsNight;

    /*
        天氣切換計時器
    */
    private float weatherTimer;


    /*
        目前平滑變化中的環境數值
    */
    private Color targetAmbientColor;
    private Color targetFogColor;

    private float targetFogDensity;
    private float targetLightIntensity;


    private void Start()
    {
        /*
            確認方向光是否已指定
        */
        if (directionalLight == null)
        {
            Debug.LogError("尚未指定 Directional Light");

            enabled = false;

            return;
        }

        /*
            開啟 Unity 霧氣
        */
        RenderSettings.fog = true;

        /*
            設定霧氣模式
        */
        RenderSettings.fogMode = fogMode;

        /*
            使用 Flat 模式後 ambientLight 顏色才會明確生效
        */
        RenderSettings.ambientMode = AmbientMode.Flat;

        /*
            遊戲開始時先判斷目前日夜
        */
        UpdateDayNightState();

        previousIsNight = isNight;

        /*
            遊戲開始立即亂數決定天氣
        */
        RandomizeWeather();

        /*
            立即套用第一個 Skybox
        */
        ApplySkybox();

        /*
            設定環境光、霧氣與方向光目標值
        */
        UpdateEnvironmentTargets();

        /*
            遊戲開始時直接套用一次
            避免一開始慢慢從錯誤顏色變化
        */
        ApplyEnvironmentImmediately();

        /*
            重設天氣計時器
        */
        weatherTimer = weatherChangeInterval;
    }


    private void Update()
    {
        /*
            持續判斷現在為白天或夜晚
        */
        UpdateDayNightState();

        /*
            日夜狀態改變時立即切換對應天空盒
        */
        if (isNight != previousIsNight)
        {
            previousIsNight = isNight;

            ApplySkybox();

            UpdateEnvironmentTargets();

            Debug.Log(
                isNight
                    ? "目前切換為夜晚"
                    : "目前切換為白天"
            );
        }

        /*
            計算天氣切換時間
        */
        weatherTimer -= Time.deltaTime;

        if (weatherTimer <= 0f)
        {
            RandomizeWeather();

            ApplySkybox();

            UpdateEnvironmentTargets();

            weatherTimer = weatherChangeInterval;
        }

        /*
            平滑更新環境光、霧氣與方向光
        */
        UpdateEnvironmentSmoothly();
    }


    /*
        依方向光角度判斷目前日夜
    */
    private void UpdateDayNightState()
    {
        /*
            太陽朝下照射時數值接近一
            太陽位於地平線附近時接近零
            太陽位於地面下方時會小於零
        */
        float sunHeight = Vector3.Dot(
            directionalLight.transform.forward,
            Vector3.down
        );

        isNight = sunHeight < nightThreshold;
    }


    /*
        亂數決定目前天氣
    */
    public void RandomizeWeather()
    {
        /*
            防止兩個機率相加超過一
        */
        float safeStormChance =
            Mathf.Clamp01(stormChance);

        float safeCloudyChance =
            Mathf.Clamp01(cloudyChance);

        /*
            若暴風雨只能在晚上出現
            白天時將暴風雨機率設為零
        */
        if (stormOnlyAtNight && !isNight)
        {
            safeStormChance = 0f;
        }

        /*
            確保暴風雨與陰天總和不超過一
        */
        safeCloudyChance = Mathf.Min(
            safeCloudyChance,
            1f - safeStormChance
        );

        float randomValue = Random.value;

        /*
            暴風雨區間
        */
        if (randomValue < safeStormChance)
        {
            currentWeather = WeatherType.Storm;
        }
        /*
            陰天區間
        */
        else if (
            randomValue <
            safeStormChance + safeCloudyChance
        )
        {
            currentWeather = WeatherType.Cloudy;
        }
        /*
            剩下的機率為晴天
        */
        else
        {
            currentWeather = WeatherType.Clear;
        }

        Debug.Log(
            "目前天氣切換為 " +
            GetWeatherChineseName(currentWeather)
        );
    }


    /*
        選擇並套用目前天氣與日夜對應的 Skybox
    */
    private void ApplySkybox()
    {
        Material selectedSkybox = null;

        switch (currentWeather)
        {
            case WeatherType.Clear:

                selectedSkybox =
                    isNight
                        ? skyboxClearNight
                        : skyboxClearDay;

                break;


            case WeatherType.Cloudy:

                selectedSkybox =
                    isNight
                        ? skyboxCloudyNight
                        : skyboxCloudyDay;

                break;


            case WeatherType.Storm:

                selectedSkybox =
                    isNight
                        ? skyboxStormNight
                        : skyboxStormDay;

                break;
        }

        if (selectedSkybox == null)
        {
            Debug.LogWarning(
                "目前天氣對應的 Skybox 材質尚未指定"
            );

            return;
        }

        RenderSettings.skybox = selectedSkybox;

        /*
            更新天空盒帶來的環境反射
        */
        DynamicGI.UpdateEnvironment();
    }


    /*
        設定目前環境效果的目標值
    */
    private void UpdateEnvironmentTargets()
    {
        switch (currentWeather)
        {
            case WeatherType.Clear:

                targetAmbientColor =
                    isNight
                        ? nightAmbient
                        : dayAmbient;

                targetFogColor =
                    isNight
                        ? nightFog
                        : dayFog;

                targetFogDensity =
                    isNight
                        ? clearNightFogDensity
                        : clearDayFogDensity;

                targetLightIntensity =
                    isNight
                        ? clearNightLightIntensity
                        : clearDayLightIntensity;

                break;


            case WeatherType.Cloudy:

                targetAmbientColor =
                    isNight
                        ? nightAmbient *
                          cloudyAmbientMultiplier
                        : dayAmbient *
                          cloudyAmbientMultiplier;

                targetFogColor =
                    isNight
                        ? cloudyNightFog
                        : cloudyDayFog;

                targetFogDensity =
                    isNight
                        ? cloudyNightFogDensity
                        : cloudyDayFogDensity;

                targetLightIntensity =
                    isNight
                        ? cloudyNightLightIntensity
                        : cloudyDayLightIntensity;

                break;


            case WeatherType.Storm:

                targetAmbientColor =
                    isNight
                        ? nightAmbient *
                          stormAmbientMultiplier
                        : dayAmbient *
                          stormAmbientMultiplier;

                targetFogColor =
                    isNight
                        ? stormNightFog
                        : stormDayFog;

                targetFogDensity =
                    isNight
                        ? stormNightFogDensity
                        : stormDayFogDensity;

                targetLightIntensity =
                    isNight
                        ? stormNightLightIntensity
                        : stormDayLightIntensity;

                break;
        }
    }


    /*
        平滑更新環境效果
    */
    private void UpdateEnvironmentSmoothly()
    {
        float transitionValue =
            Time.deltaTime * environmentTransitionSpeed;

        RenderSettings.ambientLight =
            Color.Lerp(
                RenderSettings.ambientLight,
                targetAmbientColor,
                transitionValue
            );

        RenderSettings.fogColor =
            Color.Lerp(
                RenderSettings.fogColor,
                targetFogColor,
                transitionValue
            );

        RenderSettings.fogDensity =
            Mathf.Lerp(
                RenderSettings.fogDensity,
                targetFogDensity,
                transitionValue
            );

        if (directionalLight != null)
        {
            directionalLight.intensity =
                Mathf.Lerp(
                    directionalLight.intensity,
                    targetLightIntensity,
                    transitionValue
                );
        }
    }


    /*
        遊戲開始時立即套用環境效果
    */
    private void ApplyEnvironmentImmediately()
    {
        RenderSettings.ambientLight =
            targetAmbientColor;

        RenderSettings.fogColor =
            targetFogColor;

        RenderSettings.fogDensity =
            targetFogDensity;

        if (directionalLight != null)
        {
            directionalLight.intensity =
                targetLightIntensity;
        }
    }


    /*
        手動切換成晴天
    */
    public void SetClearWeather()
    {
        currentWeather = WeatherType.Clear;

        ApplySkybox();

        UpdateEnvironmentTargets();

        weatherTimer = weatherChangeInterval;
    }


    /*
        手動切換成陰天
    */
    public void SetCloudyWeather()
    {
        currentWeather = WeatherType.Cloudy;

        ApplySkybox();

        UpdateEnvironmentTargets();

        weatherTimer = weatherChangeInterval;
    }


    /*
        手動切換成暴風雨
    */
    public void SetStormWeather()
    {
        currentWeather = WeatherType.Storm;

        ApplySkybox();

        UpdateEnvironmentTargets();

        weatherTimer = weatherChangeInterval;
    }


    /*
        立即重新亂數天氣
    */
    public void RandomWeatherNow()
    {
        RandomizeWeather();

        ApplySkybox();

        UpdateEnvironmentTargets();

        weatherTimer = weatherChangeInterval;
    }


    /*
        取得目前天氣
    */
    public WeatherType GetCurrentWeather()
    {
        return currentWeather;
    }


    /*
        取得目前是否為夜晚
    */
    public bool GetIsNight()
    {
        return isNight;
    }


    /*
        顯示天氣中文名稱
    */
    private string GetWeatherChineseName(
        WeatherType weatherType
    )
    {
        switch (weatherType)
        {
            case WeatherType.Clear:
                return "晴天";

            case WeatherType.Cloudy:
                return "陰天";

            case WeatherType.Storm:
                return "暴風雨";

            default:
                return "未知";
        }
    }
}