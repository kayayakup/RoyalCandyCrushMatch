using UnityEngine;
using GoogleMobileAds.Api;
using System;

public class GoogleAdMobController : MonoBehaviour
{
    public static GoogleAdMobController Instance;

    // Banner & Interstitial
    private BannerView bannerView;
    private InterstitialAd interstitial;

    // Replace with your real Ad Unit IDs
    private string bannerID = "";
    private string interstitialID = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Initialize AdMob
        MobileAds.Initialize(initStatus => { });

        LoadBanner();
        LoadInterstitial();
    }



    public void RestartScene()
    {
        // Initialize AdMob
        MobileAds.Initialize(initStatus => { });

        LoadBanner();
        LoadInterstitial();
    }

    // -----------------------------------------------------------
    // BANNER
    // -----------------------------------------------------------
    public void LoadBanner()
    {
        // Destroy old banner
        if (bannerView != null)
        {
            bannerView.Destroy();
        }

        bannerView = new BannerView(bannerID, AdSize.Banner, AdPosition.Bottom);

        AdRequest request = new AdRequest();

        bannerView.LoadAd(request);
    }

    public void HideBanner()
    {
        if (bannerView != null)
            bannerView.Hide();
    }

    // -----------------------------------------------------------
    // INTERSTITIAL
    // -----------------------------------------------------------
    public void LoadInterstitial()
    {

        // Destroy old ad
        if (interstitial != null)
        {
            interstitial.Destroy();
            interstitial = null;
        }

        InterstitialAd.Load(interstitialID, new AdRequest(),
            (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null)
                {
                    Debug.Log("Interstitial failed to load: " + error.GetMessage());
                    return;
                }

                interstitial = ad;
            });
        Debug.Log("SLLOADDDAda");
    }

    public void DestroyBannerAd()
    {
        bannerView.Destroy();
    }
    public void ShowInterstitialAd()
    {
        Debug.Log("SHOW INTERSSS");
        if (interstitial != null && interstitial.CanShowAd())
        {
            interstitial.Show();
            interstitial = null; // must reload after showing
            LoadInterstitial();
        }
    }
}
