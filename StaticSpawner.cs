using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaticSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ObjectInfo
    {
        public GameObject Prefab;
        [Min(0)]
        public int ObjectCount;
        public Vector3 SpawnOffset;
        public Vector3 SpawnRotation;
        public Transform ParentObject = null;
        public LayerMask SpawnableAreaLayer = ~0; 
        public LayerMask AllowOverlapLayer = 0;
        public float SpawnClearance = 0.5f;
        public Utils.Axis RandomizeSpawnDirection = Utils.Axis.None;
    }

    public List<ObjectInfo> Objects;
    public Vector3 SpawnRange = Vector3.one * 10;
    public bool SpawnOnStart = true;
    public bool DestroySpawnedObjectsOnSpawn = true;

    [Header("Debug settings")]
    public bool LogInfo = true;
    public bool ShowSpawnArea = true;
    public bool ShowSpawnTrialPosition = true;
    public bool ShowSpawnedObjects = true;
    public bool ShowClearance = true;


    private Vector3 SpawnCenter { get { return transform.position; }}

    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Dictionary<GameObject, ObjectInfo> spawnedObjectsInfo = new Dictionary<GameObject, ObjectInfo>();

    // this dictionary is for showing trial spawn positions in gizmos
    protected Dictionary<Vector3, Vector3> spawnTrialPositions = new Dictionary<Vector3, Vector3>();

    // Start is called before the first frame update
    void Start()
    {
        if (SpawnOnStart)
        {
            SpawnObjects();
        }
    }

    [ContextMenu("SpawnObjects")]
    public void SpawnObjects()
    {
        if (DestroySpawnedObjectsOnSpawn)
        {
            DestroySpawnedObjects();
        }
        if (Objects == null || Objects.Count == 0)
            return;
        foreach (var obj in Objects)
        {
            for (int i = 0; i < obj.ObjectCount; i++)
            {
                Vector3 spawnPosition = GetSpawnPosition(obj);
                if (spawnPosition == Vector3.zero)
                {
                    if (LogInfo)
                        Logging.LogWarning(this.name, "No spawn position found for " + obj.Prefab.name);
                    continue;
                }
                GameObject spawnedObject = Instantiate(obj.Prefab, spawnPosition + obj.SpawnOffset, Quaternion.Euler(obj.SpawnRotation), obj.ParentObject);
                SetRandomSpawnDirection(spawnedObject.transform, obj);

                spawnedObjects.Add(spawnedObject);
                spawnedObjectsInfo.Add(spawnedObject, obj);

            }
        }
    }


    [ContextMenu("DestroySpawnedObjects")]
    public void DestroySpawnedObjects()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj == null)
                continue;
            
            // destroy immediate if not in play mode
            if (!Application.isPlaying)
            {
                DestroyImmediate(obj);
            }
            else
            {
                Destroy(obj);
            }
        }
        spawnedObjects.Clear();
        spawnedObjectsInfo.Clear();
    }


    public void SetRandomSpawnDirection(Transform objTransform, ObjectInfo objInfo)
    {
        switch (objInfo.RandomizeSpawnDirection)
        {
            case Utils.Axis.None:
                break;
            case Utils.Axis.X:
                objTransform.eulerAngles = new Vector3(Random.Range(0, 360), objTransform.eulerAngles.y, objTransform.eulerAngles.z);
                break;
            case Utils.Axis.Y:
                objTransform.eulerAngles = new Vector3(objTransform.eulerAngles.x, Random.Range(0, 360), objTransform.eulerAngles.z);
                break;
            case Utils.Axis.Z:
                objTransform.eulerAngles = new Vector3(objTransform.eulerAngles.x, objTransform.eulerAngles.y, Random.Range(0, 360));
                break;
            case Utils.Axis.XY:
                objTransform.eulerAngles = new Vector3(Random.Range(0, 360), Random.Range(0, 360), objTransform.eulerAngles.z);
                break;
            case Utils.Axis.XZ:
                objTransform.eulerAngles = new Vector3(Random.Range(0, 360), objTransform.eulerAngles.y, Random.Range(0, 360));
                break;
            case Utils.Axis.YZ:
                objTransform.eulerAngles = new Vector3(objTransform.eulerAngles.x, Random.Range(0, 360), Random.Range(0, 360));
                break;
            case Utils.Axis.XYZ:
                objTransform.eulerAngles = new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
                break;
        }
    }


    /// <summary>
    /// returns a random position inside the spawn area, shooting a raycast from top of the spawn area downwards
    /// </summary>
    public virtual Vector3 GetSpawnPosition(ObjectInfo obj)
    {
        LayerMask SpawnableAreaLayer = obj.SpawnableAreaLayer;
        float SpawnClearance = obj.SpawnClearance;

        Vector3 spawnPos = Vector3.zero;

        // get the bounds of the spawn area
        Bounds b = new Bounds(SpawnCenter, SpawnRange - 2 * SpawnClearance * Vector3.one);

        // do a few trials to make sure we land on a spawnable area
        for (int i = 0; i < 10; i++)
        {
            // keep the y position the same
            Vector3 upperPosition = Utils.RandomRangeBetween(b.min, b.max);
            upperPosition.y = b.max.y;

            // shoot a ray downwards. Only spawn if the ray hits the spawnable area
            RaycastHit hit;
            bool hitSomething = Physics.Raycast(upperPosition, Vector3.down, out hit, b.size.y, SpawnableAreaLayer);

            // lower position is the end of the ray
            Vector3 lowerPosition = new Vector3(upperPosition.x, hit.point.y, upperPosition.z);

            if (!hitSomething)
            {
                lowerPosition = new Vector3(upperPosition.x, b.size.y, upperPosition.z);

                // log it if we are debugging
                if (ShowSpawnTrialPosition)
                {
                    spawnTrialPositions[upperPosition] = lowerPosition;
                }

            }
            
            if (hitSomething)
            {
                // then check if the spawn position overlaps with other colliders
                LayerMask otherColliderLayer = ~(SpawnableAreaLayer | obj.AllowOverlapLayer);
                // get a list of colliders that overlap with the spawn position
                Collider[] overlaps = Physics.OverlapSphere(lowerPosition, SpawnClearance, otherColliderLayer);

                if (overlaps.Length != 0)
                {
                    // log it if we are debugging
                    if (ShowSpawnTrialPosition)
                    {
                        spawnTrialPositions[upperPosition] = lowerPosition;
                    }
                    continue;
                }
                spawnPos = lowerPosition;
            }
            else
            {
                continue;
            }
            break;
        }
        return spawnPos;
    }
    


    protected virtual void OnDrawGizmos()
    {
        if (ShowSpawnArea)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(SpawnCenter, SpawnRange);
        }

        if (ShowSpawnTrialPosition)
        {
            foreach (KeyValuePair<Vector3, Vector3> pair in spawnTrialPositions)
            {
                Gizmos.color = Color.red;
                // draw a cube from the upper position to the lower position
                Gizmos.DrawCube(new Vector3(pair.Key.x, (pair.Key.y + pair.Value.y) / 2, pair.Key.z), new Vector3(1, pair.Value.y - pair.Key.y, 1));
            }
        }

        if (ShowSpawnedObjects)
        {
            Gizmos.color = Color.cyan;
            foreach (var obj in spawnedObjects)
            {
                Vector3 middlePos = obj.transform.position;
                middlePos.y = (SpawnCenter.y + SpawnRange.y / 2 + middlePos.y) / 2;
                // draw a cube from the upper position to the lower position
                Gizmos.DrawCube(middlePos, new Vector3(1, (SpawnCenter.y + SpawnRange.y / 2 - obj.transform.position.y), 1));
            }
        }

        if (ShowClearance)
        {
            Gizmos.color = Color.green;
            foreach (var obj in spawnedObjectsInfo)
            {
                // draw a wired sphere
                Gizmos.DrawWireSphere(obj.Key.transform.position, obj.Value.SpawnClearance);
            }
        }
    }
}
