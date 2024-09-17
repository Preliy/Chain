using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;

namespace Preliy.Chain
{
    public class Chain : MonoBehaviour
    {
        public float Position
        {
            get => _position;
            set => _position = value;
        }
        
        [Header("Target")]
        [SerializeField]
        private GameObject _followTarget;

        [Header("Chain Item Prefabs")]
        [SerializeField]
        private List<GameObject> _prefabs;
        [SerializeField]
        private float _itemOffset;

        [Header("Parameters")]
        [SerializeField]
        private float _position;
        [SerializeField]
        private float _length = 1f;
        [SerializeField]
        private float _radius = 0.1f;

        [Header("Settings")]
        [SerializeField]
        private bool _useMainRoot;
        [SerializeField]
        private bool _gizmos;

        [HideInInspector]
        [SerializeField]
        private List<Vector3> _points = new();

        [SerializeField]
        private List<GameObject> _items = new ();

        private float _lastPosition;
        private const float TOLERANCE_FLOAT = 1e-6f;
        private TransformAccessArray _transformAccessArray;

        private void Start()
        {
            if (_transformAccessArray.isCreated)
            {
                _transformAccessArray.Dispose();
            }
            _transformAccessArray = new TransformAccessArray(_items.Select(item => item.transform).ToArray());
            RefreshSpline();
            RefreshItems();

            if (_useMainRoot)
            {
                foreach (var item in _items)
                {
                    item.transform.parent = null;
                }
            }
        }
        
        private void OnValidate()
        {
            if (IsUpdateNeeded())
            {
                RefreshSpline();
                RefreshItems();
            }
        }

        private void OnDestroy()
        {
            _transformAccessArray.Dispose();
        }

        private void Reset()
        {
            _points = new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero};
        }

        private void LateUpdate()
        {
            if (!IsUpdateNeeded()) return;
            RefreshSpline();
            RefreshItems();
        }
        
      



        private bool IsUpdateNeeded()
        {
            if (_followTarget != null)
            {
                _position = transform.InverseTransformPoint(_followTarget.transform.position).z;
            }

            _position = Mathf.Clamp(_position, -_length * 0.5f, _length * 0.5f);
            
            if (Mathf.Abs(_position - _lastPosition) < TOLERANCE_FLOAT) return false;
            _lastPosition = _position;
            return true;
        }

        private void RefreshSpline()
        {
            var offset = _length * 0.25f + _position * 0.5f;
            _points[1] = new Vector3(0, 0, offset);
            _points[2] = new Vector3(0, _radius, offset);
            _points[3] = new Vector3(0, _radius * 2, offset);
            _points[4] = new Vector3(0, _radius * 2, _position);
        }

        private void RefreshItems()
        {
            if (!_transformAccessArray.isCreated) return;
            var positions = new NativeArray<float3>(_transformAccessArray.length, Allocator.TempJob);
            
            var transformItemJob = new TransformItemJob
            {
                Positions = positions,
                Root = new RigidTransform(transform.localToWorldMatrix),
                Length = _length,
                Position = _position,
                Radius = _radius,
                ItemOffset = _itemOffset,
                PointArcStart = _points[1],
                PointArcCenter = _points[2],
                PointArcEnd = _points[3]
            };

            var transformItemJobHandler = transformItemJob.Schedule(_transformAccessArray);
            transformItemJobHandler.Complete();

            var lookAtItemJob = new LookAtItemJob
            {
                Positions = positions
            };
            
            var lookAtItemJobHandler = lookAtItemJob.Schedule(_transformAccessArray);
            lookAtItemJobHandler.Complete();

            positions.Dispose();
        }

        [BurstCompile]
        private struct TransformItemJob : IJobParallelForTransform
        {
            public NativeArray<float3> Positions;
            [ReadOnly] public RigidTransform Root;
            [ReadOnly] public float Length;
            [ReadOnly] public float Position;
            [ReadOnly] public float Radius;
            [ReadOnly] public float ItemOffset;
            [ReadOnly] public float3 PointArcStart;
            [ReadOnly] public float3 PointArcCenter;
            [ReadOnly] public float3 PointArcEnd;

            public void Execute(int index, TransformAccess transform)
            {
                var localStartOffset = Length * 0.25f + Position * 0.5f;
                var localPosition = index * ItemOffset;
                var localArcPosition = localPosition - localStartOffset;
                var arcLength = Mathf.PI * Radius;
                float3 position;
                quaternion rotation;

                if (localArcPosition < 0)
                {
                    position = Vector3.forward * localPosition;
                    rotation = quaternion.Euler(0, 0, 0);
                }
                else if (localArcPosition < arcLength)
                {
                    var angle = localArcPosition / Radius;
                    position = RotatePointAroundPivot(PointArcStart, PointArcCenter, Vector3.left * angle);
                    rotation = quaternion.Euler(-angle, 0, 0);
                }
                else
                {
                    position = PointArcEnd + (float3)(Vector3.back * (localArcPosition - arcLength));
                    rotation = quaternion.Euler(Mathf.PI, 0, 0);
                }

                var local = new RigidTransform(rotation, position);
                var global = math.mul(Root, local);

                Positions[index] = global.pos;
                transform.SetPositionAndRotation(global.pos, global.rot);
            }
        }

        [BurstCompile]
        private struct LookAtItemJob : IJobParallelForTransform
        {
            [ReadOnly] 
            public NativeArray<float3> Positions;

            public void Execute(int index, TransformAccess transform)
            {
                if (index > Positions.Length - 2) return;
                
                var nextIndex = index + 1;
                var direction = math.normalize(Positions[nextIndex] - (float3)transform.position);
                var rotation = quaternion.LookRotation(direction, transform.rotation * Vector3.up);
                transform.rotation = rotation;
            }
        }
        
        private static float3 RotatePointAroundPivot(float3 point, float3 pivot, float3 angles)
        {
            return math.mul(quaternion.Euler(angles), point - pivot) + pivot;
        }

#if UNITY_EDITOR
        public void InstantiatePrefabs()
        {
            RefreshSpline();
            
            DestroyPrefabs();

            _items = new List<GameObject>();
            if (_prefabs.Count == 0) return;
            
            var prefabIndex = 0;
            var totalLength = _length * 0.5f + Mathf.PI * _radius;
            var count = Mathf.CeilToInt(totalLength / _itemOffset);

            for (var i = 0; i < count; i++)
            {
                var item = PrefabUtility.InstantiatePrefab(_prefabs[prefabIndex], transform) as GameObject;
                _items.Add(item);

                prefabIndex++;
                prefabIndex %= _prefabs.Count;
            }

            if (_transformAccessArray.isCreated)
            {
                _transformAccessArray.Dispose();
            }
            
            _transformAccessArray = new TransformAccessArray(_items.Select(item => item.transform).ToArray());
            
            RefreshItems();
            
            EditorUtility.SetDirty(this);
        }
#endif
        
#if UNITY_EDITOR
        public void DestroyPrefabs()
        {
            var destroyList = new List<GameObject>(_items);

            foreach (var item in destroyList)
            {
                DestroyImmediate(item); 
            }
            
            _items.Clear();
            
            EditorUtility.SetDirty(this);
        }
#endif

        private void OnDrawGizmos()
        {
            if (!_gizmos) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.blue;
            foreach (var point in _points)
            {
                Gizmos.DrawSphere(point, 0.005f);
            }

            Gizmos.DrawLine(_points[0], _points[1]);
            Gizmos.DrawLine(_points[3], _points[4]);
        }
    }
}
