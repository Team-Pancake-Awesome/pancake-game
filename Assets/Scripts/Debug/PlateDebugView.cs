using UnityEngine;

[DisallowMultipleComponent]
public class PlateDebugView : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private Plate plate;

	[SerializeField]
	private WorkdayManager workdayManager;

	[SerializeField]
	private Camera lookAtCamera;

	[Header("Ticket Layout")]
	[SerializeField]
	private Vector3 localOffset = new(0f, 1.6f, 0f);

	[SerializeField]
	private bool offsetInPlateLocalSpace = false;

	[SerializeField]
	private float characterSize = 0.08f;

	[SerializeField]
	private float worldTextScale = 0.1f;

	[SerializeField]
	private int fontSize = 32;

	[SerializeField]
	private Color textColor = Color.black;

	[SerializeField]
	private TextAnchor alignment = TextAnchor.LowerCenter;

	[SerializeField]
	private bool faceCamera = true;

	[SerializeField]
	private bool detachFromPlateScale = true;

	[SerializeField]
	private bool billboardYAxisOnly = true;

	[SerializeField]
	private Vector3 billboardEulerOffset = new(0f, 180f, 0f);

	private TextMesh ticketText;
	private bool ownsTicketObject;

	private void Awake()
	{
		if (plate == null)
		{
			plate = GetComponent<Plate>();
		}

		if (workdayManager == null)
		{
			workdayManager = FindObjectOfType<WorkdayManager>();
		}

		if (lookAtCamera == null)
		{
			lookAtCamera = Camera.main;
		}

		EnsureTicketText();
		ApplyTextStyle();
	}

	private void LateUpdate()
	{
		if (ticketText == null)
		{
			return;
		}

		UpdateTicketPlacement();

		if (faceCamera)
		{
			FaceCamera();
		}

		RefreshTicketText();
	}

	private void EnsureTicketText()
	{
		Transform existingTicketTransform = transform.Find("PlateTicketText");
		if (existingTicketTransform != null)
		{
			ticketText = existingTicketTransform.GetComponent<TextMesh>();
		}

		if (ticketText != null)
		{
			ownsTicketObject = false;
			ApplyTicketParentingMode();
			return;
		}

		GameObject ticketObject = new("PlateTicketText");
		ticketObject.transform.SetParent(transform, false);
		ticketText = ticketObject.AddComponent<TextMesh>();
		ownsTicketObject = true;

		ApplyTicketParentingMode();

		MeshRenderer renderer = ticketObject.GetComponent<MeshRenderer>();
		if (renderer != null)
		{
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.receiveShadows = false;
		}
	}

	private void ApplyTicketParentingMode()
	{
		if (ticketText == null)
		{
			return;
		}

		if (detachFromPlateScale)
		{
			ticketText.transform.SetParent(null, true);
			return;
		}

		if (ticketText.transform.parent != transform)
		{
			ticketText.transform.SetParent(transform, true);
		}
	}

	private void ApplyTextStyle()
	{
		if (ticketText == null)
		{
			return;
		}

		ticketText.anchor = alignment;
		ticketText.alignment = TextAlignment.Center;
		ticketText.fontSize = Mathf.Max(8, fontSize);
		ticketText.characterSize = Mathf.Max(0.01f, characterSize);
		ticketText.color = textColor;
		ticketText.text = string.Empty;
	}

	private void RefreshTicketText()
	{
		if (plate == null || workdayManager == null || plate.OrderId < 0)
		{
			ticketText.text = string.Empty;
			return;
		}

		if (!workdayManager.TryGetOrderIndexById(plate.OrderId, out int orderIndex))
		{
			ticketText.text = string.Empty;
			return;
		}

		if (orderIndex < 0 || orderIndex >= workdayManager.ActiveOrders.Count)
		{
			ticketText.text = string.Empty;
			return;
		}

		GuestOrder order = workdayManager.ActiveOrders[orderIndex];
		float timeLeft = order.RemainingTime(Time.time);

		ticketText.text =
			$"{order.guestName} " +
			$"#{order.orderId}\n" +
			$"{order.requiredDoneness} | {FormatToppings(order)}\n" +
			$"{timeLeft:F1}s";
	}

	private void FaceCamera()
	{
		if (lookAtCamera == null)
		{
			lookAtCamera = Camera.main;
			if (lookAtCamera == null)
			{
				return;
			}
		}

		Quaternion lookRotation;
		if (billboardYAxisOnly)
		{
			Vector3 toCamera = lookAtCamera.transform.position - ticketText.transform.position;
			toCamera.y = 0f;

			if (toCamera.sqrMagnitude <= 0.0001f)
			{
				Vector3 fallbackForward = -lookAtCamera.transform.forward;
				fallbackForward.y = 0f;
				if (fallbackForward.sqrMagnitude <= 0.0001f)
				{
					fallbackForward = Vector3.forward;
				}

				toCamera = fallbackForward;
			}

			lookRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
		}
		else
		{
			lookRotation = lookAtCamera.transform.rotation;
		}

		ticketText.transform.rotation = lookRotation * Quaternion.Euler(billboardEulerOffset);
	}

	private static string FormatToppings(GuestOrder order)
	{
		if (order == null || order.requiredToppings == null || order.requiredToppings.Count == 0)
		{
			return "no toppings";
		}

		return string.Join(", ", order.requiredToppings);
	}

	private void UpdateTicketPlacement()
	{
		Vector3 worldPosition = offsetInPlateLocalSpace
			? transform.TransformPoint(localOffset)
			: transform.position + localOffset;

		ticketText.transform.position = worldPosition;
		ApplyTicketScale();
	}

	private void ApplyTicketScale()
	{
		float safeScale = Mathf.Max(0.01f, worldTextScale);

		if (detachFromPlateScale)
		{
			ticketText.transform.localScale = Vector3.one * safeScale;
			return;
		}

		Transform parent = ticketText.transform.parent;
		if (parent == null)
		{
			ticketText.transform.localScale = Vector3.one * safeScale;
			return;
		}

		Vector3 parentLossyScale = parent.lossyScale;
		ticketText.transform.localScale = new Vector3(
			safeScale / SafeScaleAxis(parentLossyScale.x),
			safeScale / SafeScaleAxis(parentLossyScale.y),
			safeScale / SafeScaleAxis(parentLossyScale.z));
	}

	private void OnDestroy()
	{
		if (ownsTicketObject && ticketText != null)
		{
			Destroy(ticketText.gameObject);
		}
	}

	private static float SafeScaleAxis(float axis)
	{
		float absoluteAxis = Mathf.Abs(axis);
		return absoluteAxis < 0.0001f ? 0.0001f : absoluteAxis;
	}
}
