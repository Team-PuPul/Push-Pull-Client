using System.Collections.Generic;
using Mirror;
using UnityEngine;

[DefaultExecutionOrder(50)]
public class PlayerVisualInterpolator : NetworkBehaviour
{
    private sealed class TransformProxy
    {
        public Transform Source;
        public Transform Proxy;
    }

    private sealed class SpriteProxy
    {
        public SpriteRenderer Source;
        public SpriteRenderer Proxy;
    }

    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    [Min(0f)]
    private float teleportDistance = 2f;

    private readonly Dictionary<Transform, Transform> proxyBySource =
        new Dictionary<Transform, Transform>();
    private readonly List<TransformProxy> transformProxies = new List<TransformProxy>();
    private readonly List<SpriteProxy> spriteProxies = new List<SpriteProxy>();
    private readonly List<GameObject> createdProxyObjects = new List<GameObject>();
    private MaterialPropertyBlock propertyBlock;

    private Vector3 previousPosition;
    private Vector3 currentPosition;
    private bool hasPositionHistory;
    private bool proxiesInitialized;

    public Transform CameraTarget => visualRoot != null ? visualRoot : transform;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        SnapToPlayer();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SnapToPlayer();
        InitializeVisualProxies();
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer || visualRoot == null)
            return;

        if (!proxiesInitialized)
            InitializeVisualProxies();

        Vector3 playerPosition = transform.position;

        if (
            !hasPositionHistory
            || Vector3.SqrMagnitude(playerPosition - currentPosition)
                > teleportDistance * teleportDistance
        )
        {
            previousPosition = playerPosition;
            currentPosition = playerPosition;
            hasPositionHistory = true;
            visualRoot.localPosition = Vector3.zero;
            return;
        }

        previousPosition = currentPosition;
        currentPosition = playerPosition;
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer || visualRoot == null || !hasPositionHistory)
            return;

        float interpolation =
            Time.fixedDeltaTime > 0f
                ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime)
                : 1f;

        visualRoot.position = Vector3.Lerp(previousPosition, currentPosition, interpolation);

        UpdateVisualProxies();
    }

    private void OnDisable()
    {
        if (visualRoot != null)
            visualRoot.localPosition = Vector3.zero;

        RestoreSourceRenderers();
        DestroyVisualProxies();
        hasPositionHistory = false;
    }

    private void InitializeVisualProxies()
    {
        if (proxiesInitialized || visualRoot == null)
            return;

        proxyBySource.Clear();
        proxyBySource[transform] = visualRoot;

        SpriteRenderer[] sourceRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer sourceRenderer in sourceRenderers)
        {
            if (sourceRenderer.transform == visualRoot || sourceRenderer.transform.IsChildOf(visualRoot))
                continue;

            Transform proxyTransform = GetOrCreateProxyTransform(sourceRenderer.transform);
            SpriteRenderer proxyRenderer = proxyTransform.gameObject.AddComponent<SpriteRenderer>();

            CopySpriteRenderer(sourceRenderer, proxyRenderer, true);
            sourceRenderer.forceRenderingOff = true;

            spriteProxies.Add(
                new SpriteProxy
                {
                    Source = sourceRenderer,
                    Proxy = proxyRenderer,
                }
            );
        }

        proxiesInitialized = true;
        UpdateVisualProxies();
    }

    private Transform GetOrCreateProxyTransform(Transform source)
    {
        if (proxyBySource.TryGetValue(source, out Transform existingProxy))
            return existingProxy;

        Transform parentProxy = GetOrCreateProxyTransform(source.parent);
        GameObject proxyObject = new GameObject($"{source.name}_VisualProxy");
        proxyObject.layer = source.gameObject.layer;

        Transform proxy = proxyObject.transform;
        proxy.SetParent(parentProxy, false);

        proxyBySource[source] = proxy;
        createdProxyObjects.Add(proxyObject);
        transformProxies.Add(
            new TransformProxy
            {
                Source = source,
                Proxy = proxy,
            }
        );

        return proxy;
    }

    private void UpdateVisualProxies()
    {
        foreach (TransformProxy transformProxy in transformProxies)
        {
            if (transformProxy.Source == null || transformProxy.Proxy == null)
                continue;

            transformProxy.Proxy.localPosition = transformProxy.Source.localPosition;
            transformProxy.Proxy.localRotation = transformProxy.Source.localRotation;
            transformProxy.Proxy.localScale = transformProxy.Source.localScale;
            transformProxy.Proxy.gameObject.layer = transformProxy.Source.gameObject.layer;
        }

        foreach (SpriteProxy spriteProxy in spriteProxies)
        {
            if (spriteProxy.Source == null || spriteProxy.Proxy == null)
                continue;

            CopySpriteRenderer(spriteProxy.Source, spriteProxy.Proxy, false);
        }
    }

    private void CopySpriteRenderer(
        SpriteRenderer source,
        SpriteRenderer target,
        bool copyStaticProperties
    )
    {
        target.enabled = source.enabled && source.gameObject.activeInHierarchy;
        target.sprite = source.sprite;
        target.color = source.color;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.drawMode = source.drawMode;
        target.size = source.size;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.maskInteraction = source.maskInteraction;
        target.spriteSortPoint = source.spriteSortPoint;

        if (target.sharedMaterial != source.sharedMaterial)
            target.sharedMaterial = source.sharedMaterial;

        source.GetPropertyBlock(propertyBlock);
        target.SetPropertyBlock(propertyBlock);

        if (!copyStaticProperties)
            return;

        target.tileMode = source.tileMode;
        target.adaptiveModeThreshold = source.adaptiveModeThreshold;
        target.shadowCastingMode = source.shadowCastingMode;
        target.receiveShadows = source.receiveShadows;
        target.lightProbeUsage = source.lightProbeUsage;
        target.reflectionProbeUsage = source.reflectionProbeUsage;
    }

    private void RestoreSourceRenderers()
    {
        foreach (SpriteProxy spriteProxy in spriteProxies)
        {
            if (spriteProxy.Source != null)
                spriteProxy.Source.forceRenderingOff = false;
        }
    }

    private void DestroyVisualProxies()
    {
        foreach (SpriteProxy spriteProxy in spriteProxies)
        {
            if (spriteProxy.Proxy != null && spriteProxy.Proxy.transform == visualRoot)
                Destroy(spriteProxy.Proxy);
        }

        for (int i = createdProxyObjects.Count - 1; i >= 0; i--)
        {
            if (createdProxyObjects[i] != null)
                Destroy(createdProxyObjects[i]);
        }

        spriteProxies.Clear();
        transformProxies.Clear();
        createdProxyObjects.Clear();
        proxyBySource.Clear();
        proxiesInitialized = false;
    }

    private void SnapToPlayer()
    {
        Vector3 playerPosition = transform.position;

        previousPosition = playerPosition;
        currentPosition = playerPosition;
        hasPositionHistory = true;

        if (visualRoot != null)
            visualRoot.localPosition = Vector3.zero;
    }
}
