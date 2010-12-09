using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Xml;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api;
using log4net;
using ModularRex.RexFramework;
using OpenSim.Region.Physics.Manager;
using ModularRex.RexParts.RexPython;

namespace ModularRex.RexParts
{
    public class Rex_BuiltIn_Commands : LSL_Api, Rex_BuiltIn_Commands_Interface
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ModrexObjects m_rexObjects;
        private bool m_automaticLinkPermission = false;
        private IMessageTransferModule m_TransferModule = null;

        public new void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            m_ScriptDelayFactor =
                m_ScriptEngine.Config.GetFloat("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor =
                m_ScriptEngine.Config.GetFloat("ScriptDistanceLimitFactor", 1.0f);
            m_MinTimerInterval =
                m_ScriptEngine.Config.GetFloat("MinTimerInterval", 0.5f);
            m_automaticLinkPermission =
                m_ScriptEngine.Config.GetBoolean("AutomaticLinkPermission", false);

            m_TransferModule =
                    m_ScriptEngine.World.RequestModuleInterface<IMessageTransferModule>();
            AsyncCommands = new AsyncCommandManager(ScriptEngine);

            OpenSim.Region.Framework.Interfaces.IRegionModule module = World.Modules["RexObjectsModule"];
            if (module != null && module is ModrexObjects)
            {
                m_rexObjects = (ModrexObjects)module;
            }
        }

        //public void Initialize(IScriptEngine scriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        //{
        //    try
        //    {
        //        base.Initialize(scriptEngine, host, localID, itemID);
        //    }
        //    catch (Exception e)
        //    {
        //        m_log.Error("[REXSCRIPT]: Initializting rex scriptengine failed: " + e.ToString());
        //    }
        //}

        /* are in db for assets, but in UI for textures only - also this now works for just textures 
           TODO: options for which faces to affect, e.g. main &/ some individual faces */


        // This function sets the mediaurl for all textures which are in the prim to the param...
        public int rexSetTextureMediaURL(string url, int vRefreshRate)
        {
            int changed = 0;

            Primitive.TextureEntry texs = m_host.Shape.Textures;
            Primitive.TextureEntryFace texface;

            if (tryTextureMediaURLchange(texs.DefaultTexture, url, (byte)vRefreshRate))
                changed++;

            for (uint i = 0; i < 32; i++)
            {
                if (texs.FaceTextures[i] != null)
                {
                    texface = texs.FaceTextures[i];
                    //made based on the example in llPlaySound, which seems to be the only prev thing on assets         
                    //Console.WriteLine("Changing texture " + texface.TextureID.ToString());
                    if (tryTextureMediaURLchange(texface, url, (byte)vRefreshRate))
                        changed++;
                }
            }
            return changed; //number of textures changed. usually 1 i guess?
        }

        //the guts of the api method below
        private bool tryTextureMediaURLchange(Primitive.TextureEntryFace texface, string url, byte vRefreshRate)
        {
            AssetBase texasset;

            texasset = World.AssetService.Get(texface.TextureID.ToString());
            if (texasset != null)
            {
                IRegionModule module = World.Modules["AssetMediaURLModule"];
                if (module != null && module is ModRexMediaURL)
                {
                    ((ModRexMediaURL)module).SetAssetData(texface.TextureID, url, vRefreshRate);
                    ((ModRexMediaURL)module).SendMediaURLtoAll(texface.TextureID);
                }
                //Old Rex: World.UpdateAssetMediaURLRequest(texface.TextureID, texasset, url, vRefreshRate);
                return true;
            }
            else
            {
                return false;
            }

        }



        public void rexIKSetLimbTarget(string vAvatar, int vLimbId, LSL_Types.Vector3 vDest, float vTimeToTarget, float vStayTime, float vConstraintAngle, string vStartAnim, string vTargetAnim, string vEndAnim) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    Vector3 targetpos = new Vector3((float)vDest.x, (float)vDest.y, (float)vDest.z);
                    World.ForEachScenePresence(delegate(ScenePresence controller)
                    {
                        if (controller.ControllingClient is RexNetwork.RexClientViewBase)
                        {
                            ((RexNetwork.RexClientViewBase)controller.ControllingClient).RexIKSendLimbTarget(target.UUID, vLimbId, targetpos, vTimeToTarget, vStayTime, vConstraintAngle, vStartAnim, vTargetAnim, vEndAnim);
                        }
                    });
                    //World.SendRexIKSetLimbTargetToAll(target.UUID, vLimbId, targetpos, vTimeToTarget, vStayTime, vConstraintAngle, vStartAnim, vTargetAnim, vEndAnim);
                }
            }
            catch { }
        }

        public void rexPlayAvatarAnim(string vAvatar, string vAnimName, float vRate, float vFadeIn, float vFadeOut, int nRepeats, bool vbStopAnim) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    World.ForEachScenePresence(delegate(ScenePresence controller)
                    {
                        if (controller.ControllingClient is RexNetwork.RexClientViewBase)
                        {
                            ((RexNetwork.RexClientViewBase)controller.ControllingClient).SendRexAvatarAnimation(target.UUID, vAnimName, vRate, vFadeIn, vFadeOut, nRepeats, vbStopAnim);
                        }
                    });
                    //World.SendRexPlayAvatarAnimToAll(target.UUID, vAnimName, vRate, vFadeIn, vFadeOut, nRepeats, vbStopAnim);
                }
            }
            catch { }
        }

        public void rexSetAvatarMorph(string vAvatar, string vMorphName, float vWeight, float vTime) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    World.ForEachScenePresence(delegate(ScenePresence controller)
                    {
                        if (controller.ControllingClient is RexNetwork.RexClientViewBase)
                        {
                            ((RexNetwork.RexClientViewBase)controller.ControllingClient).SendRexAvatarMorph(target.UUID, vMorphName, vWeight, vTime);
                        }
                    });
                    //World.SendRexSetAvatarMorphToAll(target.UUID, vMorphName, vWeight, vTime);
                }
            }
            catch { }
        }

        public void rexPlayMeshAnim(string vPrimId, string vAnimName, float vRate, bool vbLooped, bool vbStopAnim)
        {
            try
            {
                SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimId, 10));
                if (target != null)
                {
                    World.ForEachScenePresence(delegate(ScenePresence controller)
                    {
                        if (controller.ControllingClient is RexNetwork.RexClientViewBase)
                        {
                            ((RexNetwork.RexClientViewBase)controller.ControllingClient).SendRexMeshAnimation(target.UUID, vAnimName, vRate, vbLooped, vbStopAnim);
                        }
                    });
                    //World.SendRexPlayMeshAnimToAll(target.UUID, vAnimName, vRate, vbLooped, vbStopAnim);
                }
            }
            catch { }
        }

        public void rexSetFog(string vAvatar, float vStart, float vEnd, float vR, float vG, float vB) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexFog(vStart, vEnd, vR, vG, vB);
                    }
                }
            }
            catch { }
        }

        public void rexSetWaterHeight(string vAvatar, float vHeight) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexWaterHeight(vHeight);
                    }
                }
            }
            catch { }
        }

        public void rexDrawWater(string avatar, bool draw)
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexDrawWater(draw);
                    }
                }
            }
            catch { }

        }

        public void rexSetPostProcess(string vAvatar, int vEffectId, bool vbToggle) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexPostProcess(vEffectId, vbToggle);
                    }
                }
            }
            catch { }
        }

        public void rexRttCamera(string vAvatar, int command, string name, string assetID, LSL_Types.Vector3 vPos, LSL_Types.Vector3 vLookAt, int width, int height) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    Vector3 pos = new Vector3((float)vPos.x, (float)vPos.y, (float)vPos.z);
                    Vector3 lookat = new Vector3((float)vLookAt.x, (float)vLookAt.y, (float)vLookAt.z);
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexRttCamera(command, name, new UUID(assetID), pos, lookat, width, height);
                    }
                }
            }
            catch { }
        }

        public void rexRttCameraWorld(string vAvatar, int command, string name, string assetID, LSL_Types.Vector3 vPos, LSL_Types.Vector3 vLookAt, int width, int height) // rex
        {
            try
            {
                Vector3 pos = new Vector3((float)vPos.x, (float)vPos.y, (float)vPos.z);
                Vector3 lookat = new Vector3((float)vLookAt.x, (float)vLookAt.y, (float)vLookAt.z);
                World.ForEachScenePresence(delegate(ScenePresence controller)
                {
                    if (controller.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        ((RexNetwork.RexClientViewBase)controller.ControllingClient).SendRexRttCamera(command, name, new UUID(assetID), pos, lookat, width, height);
                    }
                });
                //World.SendRexRttCameraToAll(command, name, new UUID(assetID), pos, lookat, width, height);
            }
            catch { }
        }

        public void rexSetViewport(string vAvatar, int command, string name, float vX, float vY, float vWidth, float vHeight) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexViewport(command, name, vX, vY, vWidth, vHeight);
                    }
                }
            }
            catch { }
        }

        public void rexSetAvatarOverrideAddress(string vAvatar, string vAvatarAddress) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase rexClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        rexClient.RexAvatarURLOverride = vAvatarAddress;
                        //No need to send appearance to others manually. RexClientView handles that.
                    }
                }
            }
            catch { }
        }

        public void rexToggleWindSound(string vAvatar, bool vbToggle) // rex
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(vAvatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexToggleWindSound(vbToggle);
                    }
                }
            }
            catch { }
        }
        public void rexSetClientSideEffect(string assetId, float vTimeUntilLaunch, float vTimeUntilDeath, LSL_Types.Vector3 vPos, LSL_Types.Quaternion vRot, float vSpeed)  // rex
        {
            try
            {
                Vector3 pos = new Vector3((float)vPos.x, (float)vPos.y, (float)vPos.z);
                Quaternion rot = new Quaternion((float)vRot.x, (float)vRot.y, (float)vRot.z, (float)vRot.s);
                World.ForEachScenePresence(delegate(ScenePresence controller)
                {
                    if (controller.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        ((RexNetwork.RexClientViewBase)controller.ControllingClient).SendRexClientSideEffect(assetId, vTimeUntilLaunch, vTimeUntilDeath, pos, rot, vSpeed);
                    }
                });
                //World.SendRexClientSideEffectToAll(new UUID(assetId), vTimeUntilLaunch, vTimeUntilDeath, pos, rot, vSpeed);
            }
            catch { }
        }

        public void rexSetClientSideEffect(string assetName, int assetType, float vTimeUntilLaunch, float vTimeUntilDeath, LSL_Types.Vector3 vPos, LSL_Types.Quaternion vRot, float vSpeed)  // rex
        {
            m_log.Warn("[REXSCRIPT]: rexSetCameraClientSideEffect, could not get asset by name. Use method with uuid instead");
            //try
            //{
            //   UUID tempid = World.AssetCache.ExistsAsset((sbyte)assetType, assetName);
            //   if (tempid != UUID.Zero)
            //   {
            //      rexSetClientSideEffect(tempid.ToString(), vTimeUntilLaunch, vTimeUntilDeath, vPos, vRot, vSpeed);
            //   }
            //}
            //catch { }
        }
        public void rexSetCameraClientSideEffect(string avatar, bool enable, string assetId, LSL_Types.Vector3 vPos, LSL_Types.Quaternion vRot)  // rex
        {
            try
            {
                Vector3 pos = new Vector3((float)vPos.x, (float)vPos.y, (float)vPos.z);
                Quaternion rot = new Quaternion((float)vRot.x, (float)vRot.y, (float)vRot.z, (float)vRot.s);

                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexCameraClientSideEffect(enable, new UUID(assetId), pos, rot);
                    }
                }
            }
            catch { }
        }

        public void rexSetCameraClientSideEffect(string avatar, bool enable, string assetName, int assetType, LSL_Types.Vector3 vPos, LSL_Types.Quaternion vRot)  // rex
        {
            m_log.Warn("[REXSCRIPT]: rexSetCameraClientSideEffect, could not set camera client side effets. Asset search by name disabled");
            //try
            //{
            //    UUID tempid = World.AssetCache.ExistsAsset((sbyte)assetType, assetName);
            //    if (tempid != UUID.Zero)
            //    {
            //        rexSetCameraClientSideEffect(avatar, enable, tempid.ToString(), vPos, vRot);
            //    }
            //}
            //catch { }
        }


        public string rexRaycast(LSL_Types.Vector3 vPos, LSL_Types.Vector3 vDir, float vLength, string vIgnoreId)
        {
            uint tempignoreid = 0;
            uint collid = 0;

            if (vIgnoreId.Length > 0)
                tempignoreid = System.Convert.ToUInt32(vIgnoreId, 10);

            if(World.PhysicsScene is IRexPhysicsScene)
                collid = ((IRexPhysicsScene)World.PhysicsScene).Raycast(new Vector3((float)vPos.x, (float)vPos.y, (float)vPos.z), new Vector3((float)vDir.x, (float)vDir.y, (float)vDir.z), vLength, tempignoreid);

            return collid.ToString(); 
        }

        public void rexSetAmbientLight(string avatar, LSL_Types.Vector3 lightDirection, LSL_Types.Vector3 lightColour, LSL_Types.Vector3 ambientColour)
        {
            try
            {
                Vector3 lightDir = new Vector3((float)lightDirection.x, (float)lightDirection.y, (float)lightDirection.z);
                Vector3 lightC = new Vector3((float)lightColour.x, (float)lightColour.y, (float)lightColour.z);
                Vector3 ambientC = new Vector3((float)ambientColour.x, (float)ambientColour.y, (float)ambientColour.z);

                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexSetAmbientLight(lightDir, lightC, ambientC);
                    }
                }
            }
            catch { }
        }

        public void rexSetSky(string avatar, int type, string images, float curvature, float tiling)
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexSky(type, images, curvature, tiling);
                    }
                }
            }
            catch { }
        }

        public void rexPlayFlashAnimation(string avatar, string assetId, float left, float top, float right, float bottom, float timeToDeath)
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexPlayFlashAnimation(new UUID(assetId), left, top, right, bottom, timeToDeath);
                    }
                }
            }
            catch { }
        }

        public void rexPlayFlashAnimation(string avatar, string assetName, int assetType, float left, float top, float right, float bottom, float timeToDeath)
        {
            m_log.Warn("[REXSCRIPT]: Could not play flash animation. Asset search by name disabled");
            //try
            //{
            //   UUID tempid = World.AssetCache.ExistsAsset((sbyte)assetType, assetName);
            //   if (tempid != UUID.Zero)
            //   {
            //      rexPlayFlashAnimation(avatar, tempid.ToString(), left, top, right, bottom, timeToDeath);
            //   }
            //}
            //catch { }
        }

        public void rexPreloadAssets(string avatar, List<String> vAssetsList)
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    AssetBase tempasset = null;
                    Dictionary<UUID, uint> tempassetlist = new Dictionary<UUID, uint>();

                    for (int i = 0; i < vAssetsList.Count; i++)
                    {
                        tempasset = World.AssetService.Get(new UUID(vAssetsList[i]).ToString());
                        //tempasset = World.AssetCache.FetchAsset(new UUID(vAssetsList[i]));
                        if (tempasset != null)
                            tempassetlist.Add(tempasset.FullID, (uint)tempasset.Type);
                    }
                    if (tempassetlist.Count > 0)
                    {
                        if (target.ControllingClient is RexNetwork.RexClientViewBase)
                        {
                            RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                            targetClient.SendRexPreloadAssets(tempassetlist);
                        }
                    }
                }
            }
            catch { }
        }

        public void rexPreloadAvatarAssets(string avatar, List<String> vAssetsList)
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    if (vAssetsList.Count > 0)
                    {
                        if (target.ControllingClient is RexNetwork.RexClientViewBase)
                        {
                            RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                            targetClient.SendRexPreloadAvatarAssets(vAssetsList);
                        }
                    }
                }
            }
            catch { }
        }

        public void rexForceFOV(string avatar, float fov, bool enable)
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexForceFOV(fov, enable);
                    }
                }
            }
            catch { }
        }

        public void rexForceCamera(string avatar, int forceMode, float minZoom, float maxZoom)
        {
            try
            {
                ScenePresence target = World.GetScenePresence(new UUID(avatar));
                if (target != null)
                {
                    if (target.ControllingClient is RexNetwork.RexClientViewBase)
                    {
                        RexNetwork.RexClientViewBase targetClient = (RexNetwork.RexClientViewBase)target.ControllingClient;
                        targetClient.SendRexForceCamera(forceMode, minZoom, maxZoom);
                    }
                }
            }
            catch { }
        }

        public void rexAddInitialPreloadAssets(List<String> assetList)
        {
            try
            {
                if (World.Modules.ContainsKey("RexAssetPreload"))
                {
                    RexAssetPreload module = (RexAssetPreload)World.Modules["RexAssetPreload"];
                    for (int i = 0; i < assetList.Count; i++)
                    {
                        module.AddPreloadAsset(new UUID(assetList[i]));
                    }
                }
                else
                {
                    m_log.Warn("[REXSCRIPT]: scene did not contain RexAssetPreload module. Ignoring rexAddInitialPreloadAssets");
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[REXSCRIPT]: rexAddInitialPreloadAssets threw an excption ", e);
            }
        }

        public void rexRemoveInitialPreloadAssets(List<String> assetList)
        {
            try
            {
                if (World.Modules.ContainsKey("RexAssetPreload"))
                {
                    RexAssetPreload module = (RexAssetPreload)World.Modules["RexAssetPreload"];
                    for (int i = 0; i < assetList.Count; i++)
                    {
                        module.RemovePreloadAsset(new UUID(assetList[i]));
                    }
                }
                else
                {
                    m_log.Warn("[REXSCRIPT]: scene did not contain RexAssetPreload module. Ignoring rexRemoveInitialPreloadAssets");
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[REXSCRIPT]: rexRemoveInitialPreloadAssets threw an excption ", e);
            }
        }

        public string rexGetPrimFreeData(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexData;
            }
            else
                return String.Empty;
        }

        public void rexSetPrimFreeData(string vPrimLocalId, string vData)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexData = vData;
                //RexObjects.RexObjectPart rexobject = (RexObjects.RexObjectPart)target;
                //rexobject.RexData = vData;
            
                // Need to manually replicate to all users on change
                m_rexObjects.SendPrimFreeDataToAllUsers(target.UUID, vData);
            }
            else
            {
                m_log.Warn("[REXSCRIPT]: rexSetPrimFreeData, target prim not found:" + vPrimLocalId);
            }
        }

        public bool rexGetTemporaryPrim(string vPrimLocalId)
        {
            m_log.Warn("[REXSCRIPT]: rexGetTemporaryPrim not implemented");
            return false;
            //SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            //if (target != null && target.ParentGroup != null)
            //{
            //    RexObjects.RexObjectPart rexobject = (RexObjects.RexObjectPart)target;
            //    return rexobject.ParentGroup.TemporaryPrim;
            //}
            //else
            //    return false;
        }

        public void rexSetTemporaryPrim(string vPrimLocalId, bool vbData)
        {
            m_log.Warn("[REXSCRIPT]: rexSetTemporaryPrim not implemented");
            //SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            //if (target != null && target.ParentGroup != null)
            //{
            //    RexObjects.RexObjectPart rexobject = (RexObjects.RexObjectPart)target;
            //    rexobject.ParentGroup.TemporaryPrim = vbData;
            //}
            //else
            //{
            //    m_log.Warn("[REXSCRIPT]: rexSetTemporaryPrim, target prim not found:" + vPrimLocalId);
            //}
        }

        public void rexPlayClientSound(string vAvatar, string sound, double volume)
        {
            try
            {
                ScenePresence targetavatar = World.GetScenePresence(new UUID(vAvatar));
                if (targetavatar == null)
                {
                    m_log.Warn("[REXSCRIPT]: rexPlayClientSound, target avatar not found:" + vAvatar);
                    return;
                }
                UUID soundID = UUID.Zero;
                if (!UUID.TryParse(sound, out soundID))
                {
                    ;
                    //soundID = World.AssetCache.ExistsAsset(1, sound);
                }
                if (soundID != UUID.Zero)
                    targetavatar.ControllingClient.SendPlayAttachedSound(soundID, targetavatar.ControllingClient.AgentId, targetavatar.ControllingClient.AgentId, (float)volume, 0);
                else
                {
                    m_log.Warn("[REXSCRIPT]: rexPlayClientSound, sound not found:" + sound);
                }
            }
            catch (Exception e) { m_log.Error("[REXSCRIPT]: Could not play sound file.", e); }
        }

        #region RexPrimdata variables

        public int GetRexDrawType(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return (int)rop.RexDrawType;
            }
            else
                return 0;
        }

        public void SetRexDrawType(string vPrimLocalId, int vDrawType)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexDrawType = (byte)vDrawType;
            }
            else
            {
                m_log.Warn("[REXSCRIPT]: SetRexDrawType, target prim not found:" + vPrimLocalId);
            }
        }

        public bool GetRexIsVisible(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexIsVisible;
            }
            else
                return false;
        }

        public void SetRexIsVisible(string vPrimLocalId, bool vbIsVisible)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexIsVisible = vbIsVisible;
            }
            else
            {
                m_log.Warn("[REXSCRIPT]: SetRexIsVisible, target prim not found:" + vPrimLocalId);
            }
        }

        public bool GetRexCastShadows(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexCastShadows;
            }
            else
                return false;
        }

        public void SetRexCastShadows(string vPrimLocalId, bool vbCastShadows)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexCastShadows = vbCastShadows;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexCastShadows, target prim not found:" + vPrimLocalId);
        }

        public bool GetRexLightCreatesShadows(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexLightCreatesShadows;
            }
            else
                return false;
        }

        public void SetRexLightCreatesShadows(string vPrimLocalId, bool vbLightCreates)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexLightCreatesShadows = vbLightCreates;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexLightCreatesShadows, target prim not found:" + vPrimLocalId);
        }

        public bool GetRexDescriptionTexture(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexDescriptionTexture;
            }
            else
                return false;
        }

        public void SetRexDescriptionTexture(string vPrimLocalId, bool vbDescTex)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexDescriptionTexture = vbDescTex;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexDescriptionTexture, target prim not found:" + vPrimLocalId);
        }

        public bool GetRexScaleToPrim(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexScaleToPrim;
            }
            else
                return false;
        }

        public void SetRexScaleToPrim(string vPrimLocalId, bool vbScale)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexScaleToPrim = vbScale;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexScaleToPrim, target prim not found:" + vPrimLocalId);
        }

        public float GetRexDrawDistance(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexDrawDistance;
            }
            else
                return 0;
        }

        public void SetRexDrawDistance(string vPrimLocalId, float vDist)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexDrawDistance = vDist;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexDrawDistance, target prim not found:" + vPrimLocalId);
        }

        public float GetRexLOD(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexLOD;
            }
            else
                return 0;
        }

        public void SetRexLOD(string vPrimLocalId, float vLod)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexLOD = vLod;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexLOD, target prim not found:" + vPrimLocalId);
        }

        public string GetRexMeshUUID(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexMeshUUID.ToString();
            }
            else
                return String.Empty;
        }

        public void SetRexMeshUUID(string vPrimLocalId, string vsUUID)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexMeshUUID = new UUID(vsUUID);
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexMeshUUID, target prim not found:" + vPrimLocalId);
        }

        public void SetRexMeshByName(string vPrimLocalId, string vsName)
        {
            m_log.Warn("[REXSCRIPT]: SetRexMeshByName asset search by name disabled");
            //SetRexMeshUUID(vPrimLocalId, World.AssetCache.ExistsAsset(43, vsName).ToString());
        }

        public string GetRexCollisionMeshUUID(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexCollisionMeshUUID.ToString();
            }
            else
                return String.Empty;
        }

        public void SetRexCollisionMeshUUID(string vPrimLocalId, string vsUUID)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexCollisionMeshUUID = new UUID(vsUUID);
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexCollisionMeshUUID, target prim not found:" + vPrimLocalId);
        }

        public void SetRexCollisionMeshByName(string vPrimLocalId, string vsName)
        {
            m_log.Warn("[REXSCRIPT]: SetRexCollisionMeshByName asset search by name disabled");
            //SetRexCollisionMeshUUID(vPrimLocalId, World.AssetCache.ExistsAsset(43, vsName).ToString());
        }

        public string GetRexParticleScriptUUID(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexParticleScriptUUID.ToString();
            }
            else
                return String.Empty;
        }

        public void SetRexParticleScriptUUID(string vPrimLocalId, string vsUUID)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexParticleScriptUUID = new UUID(vsUUID);
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexParticleScriptUUID, target prim not found:" + vPrimLocalId);
        }

        public void SetRexParticleScriptByName(string vPrimLocalId, string vsName)
        {
            m_log.Warn("[REXSCRIPT]: SetRexParticleScriptByName asset search by name disabled");
            //SetRexParticleScriptUUID(vPrimLocalId, World.AssetCache.ExistsAsset(47, vsName).ToString());
        }

        public string GetRexAnimationPackageUUID(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexAnimationPackageUUID.ToString();
            }
            else
                return String.Empty;
        }

        public void SetRexAnimationPackageUUID(string vPrimLocalId, string vsUUID)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexAnimationPackageUUID = new UUID(vsUUID);
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexAnimationPackageUUID, target prim not found:" + vPrimLocalId);
        }

        public void SetRexAnimationPackageByName(string vPrimLocalId, string vsName)
        {
            m_log.Warn("[REXSCRIPT]: SetRexAnimationPackageByName asset search by name disabled");
            //SetRexAnimationPackageUUID(vPrimLocalId, World.AssetCache.ExistsAsset(44, vsName).ToString());
        }

        public string GetRexAnimationName(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexAnimationName;
            }
            else
                return String.Empty;
        }

        public void SetRexAnimationName(string vPrimLocalId, string vName)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexAnimationName = vName;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexAnimationName, target prim not found:" + vPrimLocalId);
        }

        public float GetRexAnimationRate(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexAnimationRate;
            }
            else
                return 0;
        }

        public void SetRexAnimationRate(string vPrimLocalId, float vAnimRate)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexAnimationRate = vAnimRate;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexAnimationRate, target prim not found:" + vPrimLocalId);
        }

        public string RexGetMaterial(string vPrimLocalId, int vIndex)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                if (rop.RexMaterials.ContainsKey((uint)vIndex))
                    return rop.RexMaterials[(uint)vIndex].ToString();
            }
            return String.Empty;
        }

        public int RexGetMaterialCount(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexMaterials.Count;
            }
            else
                return 0;
        }

        public void RexSetMaterialUUID(string vPrimLocalId, int vIndex, string vsMatUUID)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexMaterials.AddMaterial((uint)vIndex, new UUID(vsMatUUID));
            }
            else
                m_log.Warn("[REXSCRIPT]: RexSetMaterialUUID, target prim not found:" + vPrimLocalId);
        }

        public void RexSetMaterialByName(string vPrimLocalId, int vIndex, string vsMatName)
        {
            m_log.Warn("[REXSCRIPT]: RexSetMaterialByName, asset search by name disabled");
            //SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            //if (target != null)
            //{
            //    if (target is RexObjects.RexObjectPart)
            //    {
            //        UUID tempmatid = World.AssetCache.ExistsAsset(0, vsMatName);
            //        if (tempmatid == UUID.Zero)
            //            tempmatid = World.AssetCache.ExistsAsset(45, vsMatName);

            //        RexObjects.RexObjectPart rexTarget = (RexObjects.RexObjectPart)target;
            //        rexTarget.RexMaterials.AddMaterial((uint)vIndex, tempmatid);
            //    }
            //}
            //else
            //    Console.WriteLine("[ScriptEngine]: RexSetMaterialByName, target prim not found:" + vPrimLocalId);
        }

        public string GetRexClassName(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexClassName;
            }
            else
                return String.Empty;
        }

        public void SetRexClassName(string vPrimLocalId, string vsClassName)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexClassName = vsClassName;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexClassName, target prim not found:" + vPrimLocalId);
        }

        public string GetRexSoundUUID(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexSoundUUID.ToString();

            }
            return String.Empty;
        }

        public void SetRexSoundUUID(string vPrimLocalId, string vsUUID)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexSoundUUID = new UUID(vsUUID);
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexSoundUUID, target prim not found:" + vPrimLocalId);
        }

        public void SetRexSoundByName(string vPrimLocalId, string vsName)
        {
            m_log.Warn("[REXSCRIPT]: SetRexSoundByName asset search by name disabled");
            //SetRexSoundUUID(vPrimLocalId, World.AssetCache.ExistsAsset(1, vsName).ToString());
        }

        public float GetRexSoundVolume(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {

                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexSoundVolume;

            }
            return 0;
        }

        public void SetRexSoundVolume(string vPrimLocalId, float vVolume)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {

                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexSoundVolume = vVolume;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexSoundVolume, target prim not found:" + vPrimLocalId);
        }

        public float GetRexSoundRadius(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexSoundRadius;

            }
            return 0;
        }

        public void SetRexSoundRadius(string vPrimLocalId, float vRadius)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexSoundRadius = vRadius;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexSoundRadius, target prim not found:" + vPrimLocalId);
        }

        public int GetRexSelectPriority(string vPrimLocalId)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                return rop.RexSelectPriority;

            }
            return 0;
        }

        public void SetRexSelectPriority(string vPrimLocalId, int vValue)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {

                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                rop.RexSelectPriority = vValue;
            }
            else
                m_log.Warn("[REXSCRIPT]: SetRexSelectPriority, target prim not found:" + vPrimLocalId);
        }


        #endregion

        #region Custom "overrides" of ll methods

        public int rexListen(int channelID, string name, string ID, string msg)
        {
            UUID keyID;
            UUID.TryParse(ID, out keyID);
            ScriptListener module = World.RequestModuleInterface<ScriptListener>();
            if (module != null)
            {
                return module.Listen(m_localID, m_itemID, m_host.UUID, channelID, name, keyID, msg);
            }
            else
            {
                return -1;
            }
        }

        #endregion

        public void rexAttachObjectToAvatar(string primId, string avatarId, int attachmentPoint, LSL_Types.Vector3 pos, LSL_Types.Quaternion rot, bool silent)
        {
            try
            {
                uint primLocalId = Convert.ToUInt32(primId);
                UUID avatarUUID;
                IClientAPI agent = null;
                ScenePresence avatar = null;
                if (UUID.TryParse(avatarId, out avatarUUID))
                {
                    ScenePresence presence = m_ScriptEngine.World.GetScenePresence(avatarUUID);
                    agent = presence.ControllingClient;
                    avatar = presence;
                }
                else
                {
                    uint avatarLocalId = Convert.ToUInt32(avatarId);
                    foreach (EntityBase ent in m_ScriptEngine.World.GetEntities())
                    {
                        if (ent is ScenePresence)
                        {
                            if (ent.LocalId == avatarLocalId)
                            {
                                agent = ((ScenePresence)ent).ControllingClient;
                                avatar = ((ScenePresence)ent);
                                break;
                            }
                        }
                    }
                }

                if (agent == null)
                {
                    m_log.ErrorFormat("[REXSCRIPT]: Failed to attach object to avatar. Could not find avatar {0}", avatarId);
                    return;
                }

                SceneObjectPart part = m_ScriptEngine.World.GetSceneObjectPart(primLocalId);
                if (part == null)
                {
                    m_log.Error("[REXSCRIPT] Error attaching object to avatar: Could not find object");
                    return;
                }

                Vector3 position = new Vector3((float)pos.x, (float)pos.y, (float)pos.z);
                IAttachmentsModule attachmentsModule = m_ScriptEngine.World.AttachmentsModule;
                if (attachmentsModule != null)
                {
                    attachmentsModule.AttachObject(agent, primLocalId, (uint)attachmentPoint, 
                        new Quaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s), position, silent);
                    m_ScriptEngine.World.EventManager.TriggerOnAttach(primLocalId, part.ParentGroup.GetFromItemID(), agent.AgentId);
                }
            }
            catch (Exception e)
            {
                m_log.Error("[REXSCRIPT] Error attaching object to avatar: " + e.ToString());
            }
        }

        #region EC attributes
        
        public Dictionary<string, string> rexGetECAttributes(string vPrimLocalId, string typeName, string name)
        {
            Dictionary<string, string> dest = new Dictionary<string, string>();
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                //First try to fetch from new EC Component Module
                IEntityComponentModule ec_module = World.RequestModuleInterface<IEntityComponentModule>();
                if (ec_module != null)
                {
                    ECData ec_data = ec_module.GetData(target.UUID, typeName, name);
                    if (ec_data != null)
                    {
                        string data = null;
                        System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
                        if (ec_data.DataIsString)
                        {
                            data = encoding.GetString(ec_data.Data);
                        }
                        else
                        {
                            try
                            {
                                data = encoding.GetString(ec_data.Data);
                            }
                            catch (Exception e)
                            {
                                m_log.ErrorFormat("[ECDATA]: Failed to convert EC to string. {0}, {1}, {2}. Exception {3} occurred", target.UUID, typeName, name, e.Message);
                            }
                        }

                        dest = ParseEcXmlDict(data, typeName, name);
                    }
                }

                //Then if not found, try to get from prim free data
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                if (rop != null)
                {
                    dest = ParseEcXmlDict(rop.RexData, typeName, name);
                }
            }
            else
            {
                m_log.Warn("[REXSCRIPT]: rexGetECAttributes, prim not found:" + vPrimLocalId);
            }
            
            return dest;
        }

        private Dictionary<string, string> ParseEcXmlDict(string data, string typeName, string name)
        {
            Dictionary<string, string> dest = new Dictionary<string, string>();
            // Try to parse the freedata as xml
            XmlDocument xml = new XmlDocument();
            try
            {
                xml.LoadXml(data);
                XmlNodeList components = xml.GetElementsByTagName("component");
                // Search for the component
                foreach (XmlNode node in components)
                {
                    // Check for component typename match
                    XmlAttribute typeAttr = node.Attributes["type"];
                    if ((typeAttr != null) && (typeAttr.Value == typeName))
                    {
                        String compName = "";
                        if (node.Attributes["name"] != null)
                            compName = node.Attributes["name"].Value;

                        // Check for component name match, or empty search name
                        if ((name.Length == 0) || (name == compName))
                        {
                            XmlNodeList attributes = node.ChildNodes;
                            // Fill the dictionary
                            foreach (XmlNode attrNode in attributes)
                            {
                                XmlAttribute nameAttr = attrNode.Attributes["name"];
                                XmlAttribute valueAttr = attrNode.Attributes["value"];
                                if ((nameAttr != null) && (valueAttr != null))
                                    dest[nameAttr.Value] = valueAttr.Value;
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn("[REXSCRIPT]: ParseEcXmlDict exception: " + e.Message);
            }
            return dest;
        }

        public void rexSetECAttributes(string vPrimLocalId, Dictionary<string, string> attributes, string typeName, string name)
        {
            SceneObjectPart target = World.GetSceneObjectPart(System.Convert.ToUInt32(vPrimLocalId, 10));
            if (target != null)
            {
                //First try to fetch from new EC Component Module
                IEntityComponentModule ec_module = World.RequestModuleInterface<IEntityComponentModule>();
                if (ec_module != null)
                {
                    string text = BuildXmlStringFrom("", attributes, typeName, name);
                    ECData data = ec_module.GetData(target.UUID, typeName, name);
                    if (data == null)
                    {
                        data = new ECData(target.UUID, typeName, name, text);
                    }
                    else
                    {
                        System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
                        data.Data = encoding.GetBytes(text);
                        data.DataIsString = true;
                    }
                    ec_module.SaveECData(this, data);
                    return;
                }

                //If module is not set, then try with traditional way
                RexObjectProperties rop = m_rexObjects.GetObject(target.UUID);
                if (rop != null)
                {
                    try
                    {
                        string text = BuildXmlStringFrom(rop.RexData, attributes, typeName, name);
                        // Check for reasonable data size before setting rexfreedata
                        if (text.Length <= 1000)
                        {
                            rop.RexData = text;
                            // Need to manually replicate to all users
                            m_rexObjects.SendPrimFreeDataToAllUsers(target.UUID, text);
                        }
                        else
                            m_log.Warn("[REXSCRIPT]: Too long (over 1000 chars) output data from rexSetECAttributes, not setting");
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[REXSCRIPT]: rexSetECAttributes exception: " + e.Message);
                    }
                }
            }
            else
            {
                m_log.Warn("[REXSCRIPT]: rexSetECAttributes, target prim not found:" + vPrimLocalId);
            }
        }

        private string BuildXmlStringFrom(string data, Dictionary<string, string> attributes, string typeName, string name)
        {
            // Parse the old xmldata for modifications
            XmlDocument xml = new XmlDocument();
            XmlElement compElem = null;
            XmlElement entityElem = null;
            try
            {
                xml.LoadXml(data);
                entityElem = (XmlElement)xml.GetElementsByTagName("entity")[0];
            }
            catch (Exception)
            {
                // If parsing fails, we'll just create a new empty entity element (no previous xml data or it was malformed)
                xml.RemoveAll();
            }

            // Search for the component
            if (entityElem == null)
            {
                entityElem = xml.CreateElement("entity");
                xml.AppendChild(entityElem);
            }
            XmlNodeList components = entityElem.GetElementsByTagName("component");
            foreach (XmlNode node in components)
            {
                // Check for component typename match
                XmlAttribute typeAttr = node.Attributes["type"];
                if ((typeAttr != null) && (typeAttr.Value == typeName))
                {
                    String compName = "";
                    if (node.Attributes["name"] != null)
                        compName = node.Attributes["name"].Value;

                    // Check for component name match, or empty search name
                    if ((name.Length == 0) || (name == compName))
                    {
                        compElem = (XmlElement)node;
                        break;
                    }
                }
            }
            // If component not found, prepare new
            if (compElem == null)
            {
                compElem = xml.CreateElement("component");
                compElem.SetAttribute("type", typeName);
                if (name.Length > 0)
                    compElem.SetAttribute("name", name);
                entityElem.AppendChild(compElem);
            }
            // Remove any existing attributes
            while (compElem.FirstChild != null)
            {
                compElem.RemoveChild(compElem.FirstChild);
            }
            // Fill the attributes from the dictionary
            foreach (KeyValuePair<String, String> kvp in attributes)
            {
                XmlElement attributeElem = xml.CreateElement("attribute");
                attributeElem.SetAttribute("name", kvp.Key);
                attributeElem.SetAttribute("value", kvp.Value);
                compElem.AppendChild(attributeElem);
            }
            // Convert xml to string
            StringBuilder xmlText = new StringBuilder();
            XmlWriter writer = XmlWriter.Create(xmlText);
            xml.Save(writer);
            String text = xmlText.ToString();
            // Remove the encoding tag, for some reason Naali doesn't like it
            int idx = text.IndexOf("?>");
            if ((idx >= 0) && (idx < text.Length))
                text = text.Substring(idx + 2);
            return text;
        }
        
        #endregion
    }
}
