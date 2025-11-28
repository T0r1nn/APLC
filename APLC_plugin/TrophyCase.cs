using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace APLC;

/**
 * Unused class for a case to display trophies in Trophy Mode
 */
public class TrophyCase : MonoBehaviour
{
    public InteractTrigger trigger;
    public GameObject exp;
    public GameObject ass;
    public GameObject vow;
    public GameObject off;
    public GameObject mar;
    public GameObject ren;
    public GameObject din;
    public GameObject tit;
    
    private void Start()
    {
        gameObject.GetComponent<MeshRenderer>().enabled = true;
        gameObject.GetComponent<MeshRenderer>().enabled = true;

        
        trigger = gameObject.AddComponent<InteractTrigger>();
        trigger.oneHandedItemAllowed = false;
        trigger.twoHandedItemAllowed = true;
        trigger.hoverTip = "Place trophy:";
        trigger.disabledHoverTip = "You must be holding a trophy!";
        trigger.onInteract.AddCall(new InvokableCall(this, SymbolExtensions.GetMethodInfo(()=>OnInteract())));

        List<SpawnableItemWithRarity> scrap = new();
        foreach (SelectableLevel moon in Plugin.Instance.GetTerminal().moonsCatalogueList)
        {
            scrap.AddRange(moon.spawnableScrap);
        }

        foreach (var item in scrap)
        {
            if (item.spawnableItem.name.Contains("experimentation"))
            {
                exp = item.spawnableItem.spawnPrefab;
            }
            else if (item.spawnableItem.name.Contains("assurance"))
            {
                ass = item.spawnableItem.spawnPrefab;
            }
            else if (item.spawnableItem.name.Contains("vow"))
            {
                vow = item.spawnableItem.spawnPrefab;
            }
            else if (item.spawnableItem.name.Contains("offense"))
            {
                off = item.spawnableItem.spawnPrefab;
            }
            else if (item.spawnableItem.name.Contains("march"))
            {
                mar = item.spawnableItem.spawnPrefab;
            }
            else if (item.spawnableItem.name.Contains("rend"))
            {
                ren = item.spawnableItem.spawnPrefab;
            }
            else if (item.spawnableItem.name.Contains("dine"))
            {
                din = item.spawnableItem.spawnPrefab;
            }
            else if (item.spawnableItem.name.Contains("titan"))
            {
                tit = item.spawnableItem.spawnPrefab;
            }
        }

        exp = Instantiate(exp, transform.position + Vector3.up, Quaternion.identity);
        exp.GetComponent<NetworkObject>().Spawn();
    }

    private void Update()
    {
        if (GameNetworkManager.Instance.localPlayerController.currentlyHeldObject == null) return;
        trigger.interactable =
            GameNetworkManager.Instance.localPlayerController.currentlyHeldObject.name.Contains("ap_apparatus");
    }

    private void OnInteract()
    {
        
    }
}