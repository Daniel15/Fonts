﻿using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SixLabors.Fonts
{
    /// <summary>
    /// A glyph from a particular font face.
    /// </summary>
    internal partial class GlyphInstance
    {
        private readonly ushort sizeOfEm;
        private readonly Vector2[] controlPoints;
        private readonly bool[] onCurves;
        private readonly ushort[] endPoints;
        private readonly short leftSideBearing;

        internal GlyphInstance(Vector2[] controlPoints, bool[] onCurves, ushort[] endPoints, Bounds bounds, ushort advanceWidth, short leftSideBearing, ushort sizeOfEm, ushort index)
         {
            this.sizeOfEm = sizeOfEm;
            this.controlPoints = controlPoints;
            this.onCurves = onCurves;
            this.endPoints = endPoints;
            this.Bounds = bounds;
            this.AdvanceWidth = advanceWidth;
            this.Index = index;
            this.Height = sizeOfEm - this.Bounds.Min.Y;
            this.leftSideBearing = leftSideBearing;
        }

        /// <summary>
        /// Gets the bounds.
        /// </summary>
        /// <value>
        /// The bounds.
        /// </value>
        internal Bounds Bounds { get; }

        /// <summary>
        /// Gets the width of the advance.
        /// </summary>
        /// <value>
        /// The width of the advance.
        /// </value>
        public ushort AdvanceWidth { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        /// <value>
        /// The height.
        /// </value>
        public float Height { get; }

        /// <summary>
        /// Gets the index.
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        internal ushort Index { get; }

        /// <summary>
        /// Renders the glyph to the render surface in font units relative to a bottom left origin at (0,0)
        /// </summary>
        /// <param name="surface">The surface.</param>
        /// <param name="pointSize">Size of the point.</param>
        /// <param name="location">The location.</param>
        /// <param name="dpi">The dpi.</param>
        /// <param name="lineHeight">The lineHeight the current glyph was draw agains to offset topLeft while calling out to IGlyphRenderer.</param>
        /// <exception cref="System.NotSupportedException">Too many control points</exception>
        public void RenderTo(IGlyphRenderer surface, float pointSize, Vector2 location, Vector2 dpi, float lineHeight)
        {
            location = location * dpi;

            float scaleFactor = (float)(this.sizeOfEm * 72f);

            Vector2 firstPoint = Vector2.Zero;
            Vector2 scale = new Vector2(1, -1);

            Vector2 sizeVector = (new Vector2(this.AdvanceWidth, this.Height) * pointSize * dpi) / scaleFactor;

            var hash = HashHelpers.Combine(this.GetHashCode(), sizeVector.GetHashCode());

            if (surface.BeginGlyph(new RectangleF(location.X, location.Y - (lineHeight * dpi.Y), sizeVector.X, sizeVector.Y), hash))
            {

                int startOfContor = 0;
                int endOfContor = -1;
                for (int i = 0; i < this.endPoints.Length; i++)
                {
                    surface.BeginFigure();
                    startOfContor = endOfContor + 1;
                    endOfContor = this.endPoints[i];

                    Vector2 prev = Vector2.Zero;
                    Vector2 curr = GetPoint(pointSize, dpi, scaleFactor, scale, endOfContor) + location;
                    Vector2 next = GetPoint(pointSize, dpi, scaleFactor, scale, startOfContor) + location;

                    if (this.onCurves[endOfContor])
                    {
                        surface.MoveTo(curr);
                    }
                    else
                    {
                        if (this.onCurves[startOfContor])
                        {
                            surface.MoveTo(next);
                        }
                        else
                        {
                            // If both first and last points are off-curve, start at their middle.
                            Vector2 startPoint = (curr + next) / 2;
                            surface.MoveTo(startPoint);
                        }
                    }

                    int length = (endOfContor - startOfContor) + 1;
                    for (int p = 0; p < length; p++)
                    {
                        prev = curr;
                        curr = next;
                        int currentIndex = startOfContor + p;
                        int nextIndex = startOfContor + ((p + 1) % length);
                        int prevIndex = startOfContor + (((length + p) - 1) % length);
                        next = GetPoint(pointSize, dpi, scaleFactor, scale, nextIndex) + location;

                        if (this.onCurves[currentIndex])
                        {
                            // This is a straight line.
                            surface.LineTo(curr);
                        }
                        else
                        {
                            Vector2 prev2 = prev;
                            Vector2 next2 = next;

                            if (!this.onCurves[prevIndex])
                            {
                                prev2 = (curr + prev) / 2;
                                surface.LineTo(prev2);
                            }

                            if (!this.onCurves[nextIndex])
                            {
                                next2 = (curr + next) / 2;
                            }

                            surface.LineTo(prev2);
                            surface.QuadraticBezierTo(curr, next2);
                        }
                    }

                    surface.EndFigure();
                }
            }

            surface.EndGlyph();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 GetPoint(float pointSize, Vector2 dpi, float scaleFactor, Vector2 scale, int pointIndex)
        {
            Vector2 point = scale * ((this.controlPoints[pointIndex] * pointSize * dpi) / scaleFactor); // scale each point as we go, w will now have the correct relative point size

            return point;
        }

        private static void AlignToGrid(ref Vector2 point)
        {
            Vector2 floorPoint = new Vector2(
                                        (float)Math.Floor(point.X),
                                        (float)Math.Floor(point.Y));
            Vector2 decimalPart = point - floorPoint;

            if (decimalPart.X < 0.5)
            {
                decimalPart.X = 0;
            }
            else
            {
                decimalPart.X = 1;
            }

            if (decimalPart.Y < 0.5)
            {
                decimalPart.Y = 0;
            }
            else
            {
                decimalPart.Y = 1f;
            }

            point = floorPoint + decimalPart;
        }

        private static ControlPointCollection DrawPoints(IGlyphRenderer surface, ControlPointCollection points, Vector2 point)
        {
            switch (points.Count)
            {
                case 0: break;
                case 1:
                    surface.QuadraticBezierTo(
                        points.SecondControlPoint,
                        point);
                    break;
                case 2:
                    surface.CubicBezierTo(
                        points.SecondControlPoint,
                        points.ThirdControlPoint,
                        point);
                    break;
                default:
                    throw new NotSupportedException("Too many control points");
            }
            points.Clear();
            return points;
        }

        private struct ControlPointCollection
        {
            public Vector2 SecondControlPoint;
            public Vector2 ThirdControlPoint;
            public int Count;
            public void Add(Vector2 point)
            {
                switch (this.Count++)
                {
                    case 0:
                        this.SecondControlPoint = point;
                        break;
                    case 1:
                        this.ThirdControlPoint = point;
                        break;
                    default:
                        throw new NotSupportedException("Too many control points");
                }
            }
            public void ReplaceLast(Vector2 point)
            {
                this.Count--;
                Add(point);
            }

            public void Clear()
            {
                this.Count = 0;
            }
        }
    }
}