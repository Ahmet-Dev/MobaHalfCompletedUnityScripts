using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;
using UnityEngine.Networking;

public class Player : NetworkBehaviour {

	[SerializeField] private Transform bulletSpawnPoint;
	[SerializeField] private GameObject bulletPrefab;
	[SerializeField] private float shootDistance = 10f;
	[SerializeField] private GameObject playerStar;
	[SerializeField] private TextMesh healthText;

	private Transform targetedEnemy;
	private bool enemyClicked;
	private bool walking;
	private Animator anim;
	private NavMeshAgent navAgent;
	private float nextFire;
	private float timeBetweenShots = 2f;
	private bool isAttacking = false;
	private Vector3 startingPosition;


	[SyncVar(hook = "OnHealthChanged")] private int health = 100;
	private int bulletDamage = 35;
	
	public override void OnStartLocalPlayer() {
		playerStar.SetActive(true);
		tag = "Player";
	}

	void Start () {
		Assert.IsNotNull(bulletPrefab);
		Assert.IsNotNull(bulletSpawnPoint);
		Assert.IsNotNull(playerStar);
		Assert.IsNotNull(healthText);
		anim = GetComponent<Animator>();
		navAgent = GetComponent<NavMeshAgent>();
		startingPosition = transform.position;
	}
	
	void Update () {
	

		if (!isLocalPlayer) {
			return;
		}

		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;

		if (Input.GetButtonDown("Fire2")) {
			if (Physics.Raycast(ray, out hit, 100)) {
				if (hit.collider.CompareTag("Enemy")) {
					targetedEnemy = hit.transform;
					enemyClicked = true;
				} else {
					isAttacking = false;
					walking = true;
					enemyClicked = false;
					navAgent.destination = hit.point;
					navAgent.Resume();
				}
			}
		}

		if (enemyClicked) {
			MoveAndShoot();
		}

		if (navAgent.remainingDistance <= navAgent.stoppingDistance) {
			walking = false;
		} else {
			if (!isAttacking)
				walking = true;
		}

		anim.SetBool("IsWalking", walking);
	}

	void MoveAndShoot() {
		if (targetedEnemy == null) {
			return;
		}
		navAgent.destination = targetedEnemy.position;
		
		if (navAgent.remainingDistance >= shootDistance) {
			navAgent.Resume();
			walking = true;
		}

		if (navAgent.remainingDistance <= shootDistance) {
			transform.LookAt(targetedEnemy);

			if (Time.time > nextFire) {
				isAttacking = true;
				nextFire = Time.time + timeBetweenShots;
				CmdFire();
			}
			navAgent.Stop();
			walking = false;
		}
	}

	[Command]
	void CmdFire() {
		anim.SetTrigger("Attack");
		GameObject fireball = Instantiate(bulletPrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation) as GameObject;
		fireball.GetComponent<Rigidbody>().velocity = fireball.transform.forward * 4;

		NetworkServer.Spawn(fireball);

		Destroy(fireball, 3.5f);
	}

	void OnCollisionEnter(Collision collision) {
		if (collision.gameObject.CompareTag("Bullet")) {

			TakeDamage();
		}
	}

	void TakeDamage() {

		if (!isServer)
			return;

		health -= bulletDamage;

		if (health <= 0) {
			health = 100;
			RpcRespawn();
		}

	}

	void OnHealthChanged(int updatedHealth) {
		healthText.text = updatedHealth.ToString();
	}


	[ClientRpc]
	void RpcRespawn() {

		if (isLocalPlayer) {
			transform.position = startingPosition;
		}

	}

}
