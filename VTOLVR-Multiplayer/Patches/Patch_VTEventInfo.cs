﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Harmony;
using UnityEngine;


// patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(Bullet), "KillBullet")]
class PatchBullet
{
    static bool Prefix(Bullet __instance)
    {
        
        Vector3 pos = Traverse.Create(__instance).Field("hitPoint").GetValue<Vector3>();
        Vector3 vel = Traverse.Create(__instance).Field("velocity").GetValue<Vector3>();
        Vector3 a = pos;
        a += -vel * Time.deltaTime;
        float damage = Traverse.Create(__instance).Field("damage").GetValue<float>();
      
        Hitbox hitbox = null;



        RaycastHit[] hits;
        hits = Physics.RaycastAll(pos, vel, 100.0f, 1025);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            hitbox = hit.collider.GetComponent<Hitbox>();
            if ((bool)hitbox && (bool)hitbox.actor)
            {
                PlayerManager.lastBulletHit = hitbox;
                Debug.Log("hit box bullet hit");
                ulong lastID;
                if (VTOLVR_Multiplayer.AIDictionaries.reverseAllActors.TryGetValue(hitbox.actor, out lastID))
                {

                    Debug.Log("hit player sending bullet packet");
                    Message_BulletHit hitmsg = new Message_BulletHit(PlayerManager.localUID, lastID, VTMapManager.WorldToGlobalPoint(pos), new Vector3D(vel), damage);
                    NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, hitmsg, Steamworks.EP2PSend.k_EP2PSendReliable);
                }

            }
        }

        return true;
    }
}

[HarmonyPatch(typeof(VTEventTarget), "Invoke")]
class Patch22
{
    static bool Prefix(VTEventTarget __instance)
    {


        if (Networker.isHost)
        {
            return true;
        }
        else
        {

            if (__instance.targetType == VTEventTarget.TargetTypes.Objective || __instance.targetType == VTEventTarget.TargetTypes.System)
            {
                bool shouldComplete = ObjectiveNetworker_Reciever.completeNextEvent;
                Debug.Log($"Should complete is {shouldComplete}.");
                ObjectiveNetworker_Reciever.completeNextEvent = false;
                return shouldComplete;// clients should not send kill obj packets or have them complete

            }
               
        }
        return false;
    }
}
    [HarmonyPatch(typeof(VTEventTarget), "Invoke")]
class Patch2
{
    static void Postfix(VTEventTarget __instance)
    {
        String actionIdentifier = __instance.eventName + __instance.methodName + __instance.targetID + __instance.targetType.ToString();
        /*foreach (VTEventTarget.ActionParamInfo aparam in __instance.parameterInfos)
        {
            actionIdentifier += aparam.name;
        }*/
        int hash = actionIdentifier.GetHashCode();
        if (!__instance.TargetExists())
        {
            Debug.Log("Target doesn't exist in invoke");
        }
        Message_ScenarioAction ScanarioActionOutMessage = new Message_ScenarioAction(PlayerManager.localUID, hash);
        if (Networker.isHost)
        {
            Debug.Log("Host sent Event action" + __instance.eventName + " of type " + __instance.methodName + " for target " + __instance.targetID);

            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(ScanarioActionOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        {
            Debug.Log("Client sent Event action" + __instance.eventName + " of type " + __instance.methodName + " for target " + __instance.targetID);
           // NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, ScanarioActionOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
    }
}

// patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(VTEventInfo), "LoadFromInfoNode")]
class Patch3
{
    static bool Prefix(VTEventInfo __instance, ConfigNode infoNode)
    {

        Debug.Log("bahacode scenario dictionary");
        __instance.eventName = infoNode.GetValue("eventName");
        __instance.actions = new List<VTEventTarget>();
        foreach (ConfigNode node in infoNode.GetNodes("EventTarget"))
        {
            VTEventTarget vTEventTarget = new VTEventTarget();
            vTEventTarget.LoadFromNode(node);
            __instance.actions.Add(vTEventTarget);
            Debug.Log("Compiling scenario dictonary my codd2");
            String actionIdentifier = vTEventTarget.eventName + vTEventTarget.methodName + vTEventTarget.targetID + vTEventTarget.targetType.ToString();
            /*foreach(VTEventTarget.ActionParamInfo aparam in vTEventTarget.parameterInfos)
            {
                actionIdentifier+= aparam.name;
            }*/
            Debug.Log(actionIdentifier);
            int hash = actionIdentifier.GetHashCode();
            Debug.Log("Compiling scenario dictonary adding to my dictionary");

            if (!ObjectiveNetworker_Reciever.scenarioActionsList.ContainsKey(hash))
                ObjectiveNetworker_Reciever.scenarioActionsList.Add(hash, vTEventTarget);
            else
                Debug.Log("Duplicate VT scenario actions found, we should probably rewrite the dictionary code");

        }
        return false;//dont run bahas code
    }
}






//patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(MissionObjective), "CompleteObjective")]
class Patch4
{
    static bool Prefix(MissionObjective __instance)
    {
        //prevents infinite client host pings
        //if (__instance.completed)
        //   return false;
        Debug.Log("A mission got completed we need to send it");


        // Debug.Log("sending __instance.objectiveName + __instance.objectiveID");
        String actionIdentifier = __instance.objectiveName + __instance.objectiveID;

        Debug.Log(actionIdentifier);

        //dont run corrupt objectives
        if (MissionManager.instance.IndexOfObjective(__instance) == -1)
            return false;
        Message_ObjectiveSync objOutMessage = new Message_ObjectiveSync(PlayerManager.localUID, MissionManager.instance.IndexOfObjective(__instance), ObjSyncType.EMissionCompleted);
        if (Networker.isHost && objOutMessage.objID != -1)
        {
            Debug.Log("Host sent objective complete " + __instance.objectiveID);
            ObjectiveNetworker_Reciever.completeNext = false;
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        {
            //if (VTScenario.current.objectives.GetObjective(__instance.objectiveID).objectiveType == VTObjective.ObjectiveTypes.Destroy ||
            //    VTScenario.current.objectives.GetObjective(__instance.objectiveID).objectiveType == VTObjective.ObjectiveTypes.Conditional)
             
                bool shouldComplete = ObjectiveNetworker_Reciever.completeNext;
                Debug.Log($"Should complete is {shouldComplete}.");
                ObjectiveNetworker_Reciever.completeNext = false;
                return shouldComplete;// clients should not send kill obj packets or have them complete
            
            //NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
        return true;
    }
}

//patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(MissionObjective), "FailObjective")]
class Patch5
{
    static bool Prefix(MissionObjective __instance)
    {
        //prevents infinite client host pings
        //if (__instance.failed)
        //    return true;
        Debug.Log("A mission got failed we need to send it");


        //Debug.Log("sending __instance.objectiveName + __instance.objectiveID");
        String actionIdentifier = __instance.objectiveName + __instance.objectiveID;

        Debug.Log(actionIdentifier);

        //dont run corrupt objectives
        if (MissionManager.instance.IndexOfObjective(__instance) == -1)
            return false;
        Message_ObjectiveSync objOutMessage = new Message_ObjectiveSync(PlayerManager.localUID, MissionManager.instance.IndexOfObjective(__instance), ObjSyncType.EMissionFailed);
        if (Networker.isHost && objOutMessage.objID != -1)
        {
            Debug.Log("Host sent objective fail " + __instance.objectiveID);
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        {
            
            bool shouldComplete = ObjectiveNetworker_Reciever.completeNextFailed;
            Debug.Log($"Should complete is {shouldComplete}.");
            ObjectiveNetworker_Reciever.completeNextFailed = false;
            return shouldComplete;// clients should not send kill obj packets or have them complete
            //NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
        return true;
    }
}

/*
//patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(MissionObjective), "BeginMission")]
class Patch6
{
    static bool Prefix(MissionObjective __instance)
    {
        //prevents infinite client host pings
        //if (__instance.failed)
        //    return true;
        Debug.Log("A mission got failed we need to send it");


        //Debug.Log("sending __instance.objectiveName + __instance.objectiveID");
        String actionIdentifier = __instance.objectiveName + __instance.objectiveID;

        Debug.Log(actionIdentifier);

        //dont run corrupt objectives
        if (MissionManager.instance.IndexOfObjective(__instance) == -1)
            return false;
        Message_ObjectiveSync objOutMessage = new Message_ObjectiveSync(PlayerManager.localUID, MissionManager.instance.IndexOfObjective(__instance), ObjSyncType.EMissionBegin);
        if (Networker.isHost && objOutMessage.objID != -1)
        {
            Debug.Log("Host sent objective fail " + __instance.objectiveID);
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        {

            bool shouldComplete = ObjectiveNetworker_Reciever.completeNextBegin;
            Debug.Log($"Should complete is {shouldComplete}.");
            ObjectiveNetworker_Reciever.completeNextBegin = false;
            return shouldComplete;// clients should not send kill obj packets or have them complete
            //NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
        return true;
    }
}*/


//patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(MissionObjective), "CancelObjective")]
class Patch7
{
    static bool Prefix(MissionObjective __instance)
    {
        //prevents infinite client host pings
        //if (__instance.cancelled)
        //   return true;

        Debug.Log("A mission got CancelObjective we need to send it");


        ///Debug.Log("sending __instance.objectiveName + __instance.objectiveID");
        String actionIdentifier = __instance.objectiveName + __instance.objectiveID;

        Debug.Log(actionIdentifier);

        //dont run corrupt objectives
        if (MissionManager.instance.IndexOfObjective(__instance) == -1)
            return false;
        Message_ObjectiveSync objOutMessage = new Message_ObjectiveSync(PlayerManager.localUID, MissionManager.instance.IndexOfObjective(__instance), ObjSyncType.EMissionCanceled);
        if (Networker.isHost && objOutMessage.objID != -1)
        {

            Debug.Log("Host sent objective CancelObjective " + __instance.objectiveID);
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        { 
            bool shouldComplete = ObjectiveNetworker_Reciever.completeNextCancel;
            Debug.Log($"Should complete is {shouldComplete}.");
            ObjectiveNetworker_Reciever.completeNextCancel = false;
            return shouldComplete;// clients should not send kill obj packets or have them complete
            //Debug.Log("Client sent objective CancelObjective " + __instance.objectiveID);
            // NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
        return true;
    }
}
