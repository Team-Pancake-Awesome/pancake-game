using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorkdaySummary
{
    public float dayDurationSeconds;
    public int totalOrders;
    public int servedOrders;
    public int expiredOrders;
    public float averageStars;

    public List<GuestRatingResult> ratings = new();

    public void Recalculate()
    {
        totalOrders = ratings.Count;
        servedOrders = 0;
        expiredOrders = 0;

        float starSum = 0f;
        for (int i = 0; i < ratings.Count; i++)
        {
            GuestRatingResult rating = ratings[i];
            if (rating.expired)
            {
                expiredOrders += 1;
            }
            else
            {
                servedOrders += 1;
            }

            starSum += rating.stars;
        }

        averageStars = ratings.Count > 0 ? starSum / ratings.Count : 0f;
    }
}
