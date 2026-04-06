using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ToppingController : MonoBehaviour
{
	[Header("Topping Data")]
	public PancakeToppingType toppingType = PancakeToppingType.Butter;
	[Min(0f)]
	public float amount = 1f;
	[Range(0f, 1f)]
	public float coverage = 0.2f;
	public string customName = string.Empty;

	[Header("Trigger")]
	public string pancakeTag = "Pancake";

	[Header("Placement")]
	[Min(0f)]
	public float surfaceInset = 0.02f;
	[Min(0f)]
	public float surfaceYOffset = 0.01f;

	private bool applied;

	void Awake()
	{
		// Defensive reset in case this object was instantiated from a runtime-mutated template.
		applied = false;

		Collider ownCollider = GetComponent<Collider>();
		ownCollider ??= gameObject.AddComponent<SphereCollider>();

		ownCollider.isTrigger = true;
	}

	void Reset()
	{
		Collider ownCollider = GetComponent<Collider>();
		if (ownCollider != null)
		{
			ownCollider.isTrigger = true;
		}
	}

	void OnTriggerEnter(Collider other)
	{
		TryApply(other);
	}

	void OnTriggerStay(Collider other)
	{
		TryApply(other);
	}

	void OnCollisionEnter(Collision collision)
	{
		TryApply(collision?.collider);
	}

	void OnCollisionStay(Collision collision)
	{
		TryApply(collision?.collider);
	}

	private void TryApply(Collider other)
	{
		if (applied || other == null)
		{
			return;
		}

		if (!IsPancake(other))
		{
			return;
		}

		PancakeController pancake = other.GetComponentInParent<PancakeController>();
		pancake ??= other.GetComponentInChildren<PancakeController>();

		if (pancake == null)
		{
			return;
		}

		ApplyToPancake(pancake);
	}

	private bool IsPancake(Collider other)
	{
		if (other.GetComponentInParent<PancakeController>() != null)
		{
			return true;
		}

		if (other.CompareTag(pancakeTag))
		{
			return true;
		}

		Transform root = other.transform.root;
		if (root != null && root.CompareTag(pancakeTag))
		{
			return true;
		}

		return root != null && root.GetComponentInChildren<PancakeController>() != null;
	}

	private void ApplyToPancake(PancakeController pancake)
	{
		applied = true;

		pancake.AddTopping(toppingType, amount, coverage, customName);

		Vector3 surfacePosition = GetRandomSurfacePosition(pancake);
		transform.SetParent(pancake.transform, true);
		transform.position = surfacePosition + (Vector3.up * surfaceYOffset);
		SoundManager.Instance.PlaySound(SoundCues.AddToppings, transform.position);

		GravityScript gravityScript = GetComponent<GravityScript>();
		if (gravityScript != null)
		{
			Destroy(gravityScript);
		}

		Rigidbody ownBody = GetComponent<Rigidbody>();
		if (ownBody != null)
		{
			ownBody.velocity = Vector3.zero;
			ownBody.angularVelocity = Vector3.zero;
			ownBody.isKinematic = true;
			ownBody.useGravity = false;
		}
	}

	private Vector3 GetRandomSurfacePosition(PancakeController pancake)
	{
		if (TryGetBounds(pancake, out Bounds bounds))
		{
			float maxInset = Mathf.Min(bounds.extents.x, bounds.extents.z);
			float inset = Mathf.Clamp(surfaceInset, 0f, maxInset);

			float minX = bounds.min.x + inset;
			float maxX = bounds.max.x - inset;
			float minZ = bounds.min.z + inset;
			float maxZ = bounds.max.z - inset;

			float x = Random.Range(minX, maxX);
			float z = Random.Range(minZ, maxZ);
			return new Vector3(x, bounds.max.y, z);
		}

		return pancake.transform.position;
	}

	private static bool TryGetBounds(PancakeController pancake, out Bounds bounds)
	{
		Renderer renderer = pancake.GetComponentInChildren<Renderer>();
		if (renderer != null)
		{
			bounds = renderer.bounds;
			return true;
		}

		Collider pancakeCollider = pancake.GetComponentInChildren<Collider>();
		if (pancakeCollider != null)
		{
			bounds = pancakeCollider.bounds;
			return true;
		}

		bounds = default;
		return false;
	}
}
