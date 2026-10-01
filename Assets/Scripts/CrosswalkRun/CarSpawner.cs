using System.Collections;
using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 도로 차선에서 랜덤한 속도와 주기로 자동차를 생성하는 스포너입니다.
    /// 카메라의 난이도 배수에 따라 차량 속도와 생성 주기를 동적으로 스케일링합니다.
    /// </summary>
    public class CarSpawner : MonoBehaviour
    {
        [Header("차량 프리팹 (비어있으면 기본 큐브 차량 생성)")]
        [SerializeField] private GameObject carPrefab;

        [Header("기본 스폰 주기 (초)")]
        [SerializeField] private float minSpawnInterval = 1.6f;
        [SerializeField] private float maxSpawnInterval = 3.2f;

        [Header("기본 차량 속도")]
        [SerializeField] private float minSpeed = 9f;
        [SerializeField] private float maxSpeed = 16f;

        [Header("차량 진행 방향")]
        [Tooltip("오른쪽(1, 0, 0) 또는 왼쪽(-1, 0, 0)")]
        [SerializeField] private Vector3 moveDirection = Vector3.right;

        [Header("차량 유지 시간")]
        [SerializeField] private float carLifeTime = 10f;

        [Header("난이도 연동")]
        [SerializeField] private bool scaleWithCameraDifficulty = true;

        private Coroutine spawnCoroutine;
        private AutoScrollCamera scrollCamera;

        public void Configure(Vector3 direction, float baseMinSpeed, float baseMaxSpeed, float baseMinInterval, float baseMaxInterval)
        {
            moveDirection = direction.normalized;
            minSpeed = baseMinSpeed;
            maxSpeed = baseMaxSpeed;
            minSpawnInterval = baseMinInterval;
            maxSpawnInterval = baseMaxInterval;
        }

        private void Start()
        {
            if (scaleWithCameraDifficulty)
            {
                scrollCamera = FindObjectOfType<AutoScrollCamera>();
            }

            StartSpawning();
        }

        public void StartSpawning()
        {
            if (spawnCoroutine == null)
            {
                spawnCoroutine = StartCoroutine(SpawnRoutine());
            }
        }

        public void StopSpawning()
        {
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }
        }

        private IEnumerator SpawnRoutine()
        {
            yield return new WaitForSeconds(Random.Range(0.2f, minSpawnInterval));

            while (true)
            {
                SpawnCar();

                float difficulty = GetCurrentDifficulty();
                // 난이도가 올라갈수록 스폰 간격 단축 (최대 50% 단축 제한)
                float intervalMultiplier = Mathf.Max(0.5f, 1f / Mathf.Sqrt(difficulty));
                float nextInterval = Random.Range(minSpawnInterval, maxSpawnInterval) * intervalMultiplier;

                yield return new WaitForSeconds(nextInterval);
            }
        }

        public GameObject SpawnCar()
        {
            GameObject car;

            if (carPrefab != null)
            {
                car = Instantiate(carPrefab, transform.position, Quaternion.identity);
            }
            else
            {
                car = CreateDefaultCubeCar();
                car.transform.position = transform.position;
            }

            CarObstacle obstacle = car.GetComponent<CarObstacle>();
            if (obstacle == null)
            {
                obstacle = car.AddComponent<CarObstacle>();
            }

            float difficulty = GetCurrentDifficulty();
            // 난이도가 올라갈수록 차량 속도 상승
            float scaledMin = minSpeed * difficulty;
            float scaledMax = maxSpeed * difficulty;
            float randomSpeed = Random.Range(scaledMin, scaledMax);

            obstacle.Initialize(randomSpeed, moveDirection, carLifeTime);

            return car;
        }

        private float GetCurrentDifficulty()
        {
            if (scaleWithCameraDifficulty && scrollCamera != null)
            {
                return scrollCamera.DifficultyMultiplier;
            }
            return 1.0f;
        }

        private GameObject CreateDefaultCubeCar()
        {
            GameObject cubeCar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeCar.name = "Cube_Car";
            cubeCar.transform.localScale = new Vector3(2.2f, 1.2f, 3.8f);

            Rigidbody rb = cubeCar.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Renderer renderer = cubeCar.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color[] carColors = new Color[]
                {
                    new Color(0.9f, 0.2f, 0.2f), // Red
                    new Color(0.2f, 0.5f, 0.9f), // Blue
                    new Color(0.95f, 0.8f, 0.1f), // Yellow
                    new Color(0.25f, 0.8f, 0.4f), // Green
                    new Color(0.9f, 0.9f, 0.9f), // White
                    new Color(0.2f, 0.2f, 0.2f)  // Dark Grey
                };
                renderer.material.color = carColors[Random.Range(0, carColors.Length)];
            }

            return cubeCar;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            Gizmos.color = Color.red;
            Vector3 dir = moveDirection.normalized * 3f;
            Gizmos.DrawRay(transform.position, dir);
        }
    }
}
