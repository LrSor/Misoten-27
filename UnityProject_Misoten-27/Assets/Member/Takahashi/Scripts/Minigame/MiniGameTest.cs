using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class MiniGameTest : MiniGame
{
    //test
    public float countNum = 10;
    private float currentNum;
    [SerializeField] private Text count;

    protected override void Start()
    {
        base.Start();

        currentNum = countNum;
        count.gameObject.SetActive(false);
    }

    //UpdateMiniGame‚ðoverride
    protected override void UpdateMiniGame()
    {
        currentNum -= Time.deltaTime;
        count.text = currentNum.ToString("00");
        if (currentNum <= 0)
        {
            FinishMiniGame();
        }
    }

    public override void Activate()
    {
        base.Activate();
        count.gameObject.SetActive(true);
    }

    public override void Deactivate()
    {
        base.Deactivate();
        count.gameObject.SetActive(false);
    }
}
