using UnityEngine;

namespace Fighter403.UI
{
    internal readonly struct CardAttackArea
    {
        private const int Segments = 48;
        internal const int MaxOutlinePoints = Segments + 3;

        private readonly CardHitShape shape;
        private readonly Vector2 center, direction, end;
        private readonly float range, halfAngle, halfWidth;

        internal CardAttackArea(CardDefinition card, Vector2 center, Vector2 direction, Vector2? dashEnd = null)
        {
            shape = card.HitShape;
            this.center = center;
            this.direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            end = dashEnd ?? center + this.direction * card.Distance;
            range = shape == CardHitShape.Landing ? card.ImpactRadius : card.Distance;
            halfAngle = card.HalfAngle;
            halfWidth = shape == CardHitShape.Dash ? card.DashHalfWidth : card.TriangleHalfWidth;
        }

        internal CardAttackArea(Vector2 center, float radius)
        {
            shape = CardHitShape.Circle;
            this.center = center;
            direction = Vector2.right;
            end = center;
            range = radius;
            halfAngle = halfWidth = 0f;
        }

        private Vector2 Side => new(-direction.y, direction.x);
        private Vector2 TriangleLeft => end + Side * halfWidth;
        private Vector2 TriangleRight => end - Side * halfWidth;

        internal bool Overlaps(Vector2 target, float targetRadius)
        {
            Vector2 offset = target - center;
            switch (shape)
            {
                case CardHitShape.Dash:
                    return Within(DistanceToSegmentSquared(target, center, end), halfWidth + targetRadius);
                case CardHitShape.Circle:
                case CardHitShape.Landing:
                    return Within(offset.sqrMagnitude, range + targetRadius);
                case CardHitShape.Triangle:
                    float depth = Vector2.Dot(direction, offset);
                    bool inside = depth >= 0f && depth <= range &&
                        Mathf.Abs(Vector2.Dot(Side, offset)) * range <= depth * halfWidth;
                    return (range > 0f && inside) || SegmentTouches(target, targetRadius, center, TriangleLeft) ||
                        SegmentTouches(target, targetRadius, TriangleLeft, TriangleRight) ||
                        SegmentTouches(target, targetRadius, TriangleRight, center);
                case CardHitShape.Arc:
                    if (!Within(offset.sqrMagnitude, range + targetRadius)) return false;
                    if (Vector2.Angle(direction, offset) <= halfAngle) return true;
                    float heading = Mathf.Atan2(direction.y, direction.x);
                    float sweep = halfAngle * Mathf.Deg2Rad;
                    return SegmentTouches(target, targetRadius, center, center + Polar(heading - sweep) * range) ||
                        SegmentTouches(target, targetRadius, center, center + Polar(heading + sweep) * range);
                default:
                    return false;
            }
        }

        // The renderer closes the outline between the last and first vertices.
        internal int WriteOutline(Vector3[] points, float z)
        {
            switch (shape)
            {
                case CardHitShape.Dash:
                    if ((end - center).sqrMagnitude <= 0.00000001f) return 0;
                    Vector2 path = end - center;
                    float pathHeading = Mathf.Atan2(path.y, path.x);
                    const int cap = Segments / 2;
                    for (int i = 0; i <= cap; i++)
                    {
                        Vector2 radial = Polar(pathHeading + Mathf.PI * (i / (float)cap - 0.5f)) * halfWidth;
                        points[i] = AtHeight(end + radial, z);
                        points[cap + 1 + i] = AtHeight(center - radial, z);
                    }
                    return 2 * (cap + 1);
                case CardHitShape.Triangle:
                    points[0] = AtHeight(center, z);
                    points[1] = AtHeight(TriangleLeft, z);
                    points[2] = AtHeight(TriangleRight, z);
                    return 3;
                case CardHitShape.Arc:
                case CardHitShape.Circle:
                case CardHitShape.Landing:
                    bool sector = shape == CardHitShape.Arc && halfAngle < 180f;
                    float sweep = sector ? halfAngle * Mathf.Deg2Rad : Mathf.PI;
                    float heading = Mathf.Atan2(direction.y, direction.x);
                    int first = sector ? 1 : 0;
                    if (sector) points[0] = AtHeight(center, z);
                    int arcPoints = sector ? Segments + 1 : Segments;
                    for (int i = 0; i < arcPoints; i++)
                    {
                        float angle = heading + Mathf.Lerp(-sweep, sweep, i / (float)Segments);
                        points[first + i] = AtHeight(center + Polar(angle) * range, z);
                    }
                    return first + arcPoints;
                default:
                    return 0;
            }
        }

        private static Vector3 AtHeight(Vector2 point, float z) => new(point.x, point.y, z);
        private static Vector2 Polar(float angle) => new(Mathf.Cos(angle), Mathf.Sin(angle));
        private static bool Within(float squaredDistance, float radius) => squaredDistance <= radius * radius;
        internal static bool SegmentTouches(Vector2 point, float radius, Vector2 from, Vector2 to) =>
            Within(DistanceToSegmentSquared(point, from, to), radius);

        private static float DistanceToSegmentSquared(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 segment = to - from;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.00000001f) return (point - from).sqrMagnitude;
            float progress = Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSquared);
            return (point - (from + segment * progress)).sqrMagnitude;
        }
    }
}
