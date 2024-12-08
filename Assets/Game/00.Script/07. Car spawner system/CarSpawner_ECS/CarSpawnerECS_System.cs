

using System;
using System.Collections.Generic;
using Game._00.Script._05._Manager;
using Game._00.Script.NewPathFinding;
using Game._03._Scriptable_Object;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SocialPlatforms;
namespace Game._00.Script.ECS_Test.FactoryECS
{

   [BurstCompile]
   [UpdateInGroup (typeof(SimulationSystemGroup))]
   public partial class CarSpawnSystem : SystemBase, IObserver
    {
        private PathRequestManager _pathRequestManager;
        private bool _isNotified = false;
        protected override void OnCreate()
        {
            RequireForUpdate<SpawnGameObjectHolder>();
        }
        
        public void OnNotified(object data, string flag)
        {
            if (data is ValueTuple<BuildingBase, BuildingBase> && flag == NotificationFlags.SpawnCar)
            {
                if (!_isNotified) //To avoid duplicate OnNotified call
                {
                    _isNotified = true;
                }
                else
                {
                    return;
                }
                
                ValueTuple<BuildingBase, BuildingBase> startEndBuildings = (ValueTuple<BuildingBase, BuildingBase>)data;
                Vector3[] waypoints = _pathRequestManager.GetPathWaypoints(startEndBuildings.Item1.WorldPosition, startEndBuildings.Item2.WorldPosition);
                BlobAssetReference<BlobArray<float3>> WaypointsBlob = CreateWaypointsBlob(waypoints);
                
     
                SpawnCarEntity(ObjectFlags.Car, new SpawnData()
                {
                    StartPos = new float3(startEndBuildings.Item1.WorldPosition.x, startEndBuildings.Item1.WorldPosition.y, 0),
                    EndPos = new float3(startEndBuildings.Item2.WorldPosition.x, startEndBuildings.Item2.WorldPosition.y, 0),
                    Waypoints = WaypointsBlob, 
                });
              
                
                _isNotified = false;
            }
        }

        protected override void OnUpdate()
        {
            //Delay time to get the _pathRequestManager because OnCreate() is called before Awake() to initialize the class
            if (_pathRequestManager == null)
            {
                _pathRequestManager = GameManager.Instance.PathRequestManager;
            }
            else
            {
                this.Enabled = false;
            }
        }
        
        /// <summary>
        /// Using entity manager to run this in main thread, moving car in jobs => more optimized
        /// </summary>
        /// <param name="objectFlags"></param>
        /// <param name="spawnData"></param>
        public void SpawnCarEntity(string objectFlags, SpawnData spawnData)
        {  
            SpawnGameObjectHolder objectHolder = SystemAPI.GetSingleton<SpawnGameObjectHolder>();

            if (objectFlags == ObjectFlags.Car)
            {
                Entity spawnedEntity = EntityManager.Instantiate(objectHolder.Entity1);

                float3 spawnPosition = new float3(spawnData.StartPos.x, spawnData.StartPos.y, 0);

                // Set components directly using EntityManager
                EntityManager.SetComponentData(spawnedEntity, new LocalTransform
                {
                    Position = spawnPosition,
                    Rotation = quaternion.identity,
                    Scale =  1f,
                });

                EntityManager.AddComponentData(spawnedEntity, new FollowPathData()
                {
                    WaypointsBlob = spawnData.Waypoints
                });

                EntityManager.SetComponentData(spawnedEntity, new CanRun()
                {
                    Value = true,
                });
            }
        }
        
        /// <summary>
        /// Convert Vector3[] waypoints to BlobAsset more optimized for ECS and Job system
        /// </summary>
        /// <param name="waypoints"></param>
        /// <returns></returns>
        public BlobAssetReference<BlobArray<float3>> CreateWaypointsBlob(Vector3[] waypoints)
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BlobArray<float3>>();
                var waypointArray = builder.Allocate(ref root, waypoints.Length);

                for (int i = 0; i < waypoints.Length; i++)
                {
                    waypointArray[i] = new float3(waypoints[i].x, waypoints[i].y, waypoints[i].z);
                }

                return builder.CreateBlobAssetReference<BlobArray<float3>>(Allocator.Persistent);
            }
        }
    }

   
}