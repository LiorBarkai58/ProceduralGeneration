using UnityEngine;
using System.Collections.Generic;

public class WFCNode : MonoBehaviour
{
    [SerializeField] private List<WFCNodeOption> _domain;
    [SerializeField] private bool _isCollapsed = false;
    [SerializeField] private WFCNodeOption CollapsedSO;
    [SerializeField] private GameObject CollapsedObject;
    [SerializeField] private int WFCOptionRotations = 0;
    [SerializeField] private WFCGrid ParentGrid;
    [SerializeField] private float CachedEntropy = 1;

    public void InitializeNode(WFCGrid parent)
    {
        ParentGrid = parent;
    }

    public void ResetDomain()
    {
        _domain = ParentGrid.GetDeafultDomain();
    }

    public bool IsCollapsed() => _isCollapsed;

    public List<WFCNodeOption> GetLegalDomainAtEdge(NeighborDirection direction)
    {
        if (_isCollapsed) return CollapsedSO.GetLegatNeighbors(direction, WFCOptionRotations);
        else return ParentGrid.GetDeafultDomain();
    }

    public float GetEntropy()
    {
        // Collapsed or empty/singleton domain => zero uncertainty
        if (_isCollapsed || _domain == null || _domain.Count <= 1)
        {
            CachedEntropy = 0f;
            return 0f;
        }

        float sumW = 0.0f;
        float sumWLogW = 0.0f;
        int validCount = 0;

        for (int i = 0; i < _domain.Count; i++)
        {
            var opt = _domain[i];
            if (opt == null) continue;
             
            float w = opt.GetWeight();
            if (w <= 0.0) continue; 

            sumW += w;
            sumWLogW += w * Mathf.Log(w);
            validCount++;
        }

        float Hbits;
        if (validCount <= 1 || sumW <= 0.0)
        {
            Hbits = 0f;
        }
        else
        {
            // Shannon entropy: H = log S − (Σ w log w)/S   (natural log)
            // Convert nats -> bits by multiplying 1/ln(2)
            float Hnats = Mathf.Log(sumW) - (sumWLogW / sumW);
            Hbits = (float)(Hnats * (1.0 / Mathf.Log(2)));
        }

        // Small random tie-breaker (does not affect ordering materially)
        Hbits += Random.value * 1e-6f;

        CachedEntropy = Hbits;
        return Hbits;
    }



    public float GetCachedEntropy()
    {
        return CachedEntropy;
    }
}
