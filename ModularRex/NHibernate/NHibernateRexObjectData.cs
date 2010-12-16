﻿/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenMetaverse;
using log4net;
using NHibernate;
using NHibernate.Criterion;
using OpenSim.Framework;
using Environment = NHibernate.Cfg.Environment;
using ModularRex.RexFramework;

namespace ModularRex.NHibernate
{
    /// <summary>
    /// A RexObjectData Interface to the NHibernate database
    /// </summary>
    public class NHibernateRexObjectData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public bool Inizialized = false;

        public NHibernateManager manager;
        private bool m_nullStorage = false;

        public void Initialise(string connect)
        {
            m_log.InfoFormat("[NHIBERNATE] Initializing NHibernateRexObjectData");
            if (connect.ToLower() == "null")
            {
                m_nullStorage = true;
                Inizialized = true;
                return;
            }
            
            Assembly assembly = GetType().Assembly;
            manager = new NHibernateManager(connect, "RexObjectData", assembly);
            Inizialized = true;
        }

        public void Dispose() { }

        private void SaveOrUpdate(RexObjectProperties p)
        {
            try
            {
                IList<RexMaterialsDictionaryItem> templist = p.RexMaterialDictionaryItems;
                if (p.RexMaterialDictionaryItems == null)
                {
                    p.RexMaterialDictionaryItems = new List<RexMaterialsDictionaryItem>();
                }
                int i;
                ISession session = manager.GetSession();
                ICriteria criteria = session.CreateCriteria(typeof(RexObjectProperties)).Add(Restrictions.Eq("ParentObjectID", p.ParentObjectID)).SetProjection(Projections.Count("ParentObjectID"));
                if (session == null || criteria == null)
                {
                    i = 0;
                }
                else
                {
                    object tmp = criteria.UniqueResult(); //Q: how many objcts with same id
                    if (tmp is int && tmp != null)
                    {
                        i = (int)tmp;
                    }
                    else
                    {
                        i = 0;
                    }
                }
                session.Close();
                if (i != 0) //A: more than zero, update instead of insert
                {
                    m_log.DebugFormat("[NHIBERNATE] updating RexObjectProperties {0}", p.ParentObjectID);
                    manager.Update(p);
                }
                else //B: zero, insert
                {
                    m_log.DebugFormat("[NHIBERNATE] saving RexObjectProperties {0}", p.ParentObjectID);
                    p.RexMaterialDictionaryItems = new List<RexMaterialsDictionaryItem>();
                    manager.Insert(p);
                }

                //Process materials after inserting/updateing other data. New items have material if they are copied!
                if (templist != null && templist.Count > 0)
                {
                    ProcessMaterials(p, templist);
                }
            }
            catch (Exception e)
            {
                m_log.Error("[NHIBERNATE] issue saving RexObjectProperties: "+ e.Message + e.Source + e.StackTrace);
            }
        }

        private void ProcessMaterials(RexObjectProperties p, IList<RexMaterialsDictionaryItem> rexMaterials)
        {
            try
            {
                foreach (RexMaterialsDictionaryItem item in rexMaterials)
                {
                    if (item.ID == 0)
                    {
                        ISession session = manager.GetSession();
                        ICriteria criteria2 = session.CreateCriteria(typeof(RexMaterialsDictionaryItem));
                        criteria2.Add(Restrictions.Eq("RexObjectUUID", p.ParentObjectID));
                        criteria2.Add(Restrictions.Eq("Num", item.Num));
                        criteria2.SetMaxResults(1);
                        List<RexMaterialsDictionaryItem> list = (List<RexMaterialsDictionaryItem>)criteria2.List<RexMaterialsDictionaryItem>();
                        session.Close();
                        if (list.Count == 0)
                        {
                            item.RexObjectUUID = p.ParentObjectID;
                            manager.Insert(item);
                        }
                        else
                        {
                            list[0].AssetID = item.AssetID;
                            list[0].AssetURI = item.AssetURI;
                            manager.Update(list[0]);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Error("[NHibernate]: Exception was thrown while processing RexObjectMaterials" + ex);
            }
        }

        /// <summary>
        /// Adds an object into region storage
        /// </summary>
        /// <param name="obj">the object</param>
        /// <param name="regionUUID">the region UUID</param>
        public void StoreObject(RexObjectProperties obj)
        {
            lock (manager)
            {
                if (m_nullStorage)
                    return;
                try
                {
                    SaveOrUpdate(obj);
                }
                catch (Exception e)
                {
                    m_log.Error("Can't save: ", e);
                }
            }
        }

        public RexObjectProperties LoadObject(UUID uuid)
        {
            if (m_nullStorage)
                return null;
            try
            {
                RexObjectProperties obj = null;
                ICriteria criteria = manager.GetSession().CreateCriteria(typeof(RexObjectProperties));
                criteria.Add(Expression.Eq("ParentObjectID", uuid));
                criteria.AddOrder(Order.Asc("ParentObjectID"));

                foreach (RexObjectProperties p in criteria.List())
                {
                    // root part
                    if (p.ParentObjectID == uuid)
                    {
                        return p;
                    }
                }

                return obj;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[NHIBERNATE]: Failed loading RexObject with id {0}. Exception {1} ", uuid, e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Removes an object from region storage
        /// </summary>
        /// <param name="obj">the object</param>
        /// <param name="regionUUID">the region UUID</param>
        public void RemoveObject(UUID obj)
        {
            if (m_nullStorage)
                return;
            RexObjectProperties g = LoadObject(obj);
            if (g != null)
                manager.Delete(g);
            else
                m_log.Warn("[NHIBERNATE]: Could not delete null object");

            m_log.InfoFormat("[REGION DB]: Removing obj: {0}", obj.Guid);
        }

        public void Shutdown()
        {
            //session.Flush();
        }
    }
}
