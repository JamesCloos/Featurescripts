FeatureScript 2796;
import(path : "onshape/std/common.fs", version : "2796.0");
import(path : "onshape/std/geometry.fs", version : "2796.0");

annotation { "Feature Type Name" : "Tangent Connect", 
             "Feature Type Description": "Draw true external tangent lines between circles of any size (experimental version)." }
export const tangentConnectExperimental = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Circles", "Item name" : "Circle", "Driven query" : "circle", "Item label template" : "#circle", "Description" : "Select circles in clockwise order" }
        definition.circles is array;
        for (var circ in definition.circles)
        {
            annotation { "Name" : "Circle", "Filter" : EntityType.EDGE && GeometryType.CIRCLE, "MaxNumberOfPicks" : 1 }
            circ.circle is Query;
            
            annotation { "Name" : "Offset distance" }
            isLength(circ.offset, { (inch) : [0, 0.25, 500] } as LengthBoundSpec);
        }
        
        annotation { "Name" : "Extrude plate", "Default" : false }
        definition.extrudePlate is boolean;
        
        if (definition.extrudePlate)
        {
            annotation { "Name" : "Depth" }
            isLength(definition.depth, { (inch) : [0, 0.25, 500] } as LengthBoundSpec);
            
            annotation { "Name" : "Opposite direction", "UIHint" : [UIHint.OPPOSITE_DIRECTION] }
            definition.flipDirection is boolean;
            
            annotation { "Name" : "Offset extrude", "Default" : false }
            definition.hasOffset is boolean;
            
            if (definition.hasOffset)
            {
                annotation { "Name" : "Offset distance" }
                isLength(definition.offsetDistance, LENGTH_BOUNDS);
                
                annotation { "Name" : "Opposite offset direction", "UIHint" : [UIHint.OPPOSITE_DIRECTION] }
                definition.flipOffsetDirection is boolean;
            }
            
            annotation { "Name" : "Keep sketches", "Default" : false }
            definition.keepSketches is boolean;
            
            annotation { "Name" : "Keep holes", "Default" : false }
            definition.keepHoles is boolean;
            
            if (definition.keepHoles)
            {
                annotation { "Name" : "Hole circles", "Filter" : EntityType.EDGE && GeometryType.CIRCLE }
                definition.holeCircles is Query;
            }
        }
        
        // Debug info (commented out but code remains for troubleshooting)
        // annotation { "Name" : "Show debug info", "Default" : false }
        // definition.showDebug is boolean;
    }
    {
        // Debug mode (can be enabled for troubleshooting)
        var showDebug = false; // Set to true to enable console output
        
        // Get all selected circles
        if (size(definition.circles) < 2)
        {
            throw regenError("Select at least 2 circles", ["circles"]);
        }
        
        // Extract circle data
        var circles = [];
        var offsetCircles = [];
        var sketchPlane;
        
        for (var i = 0; i < size(definition.circles); i += 1)
        {
            var circItem = definition.circles[i];
            var circQuery = circItem.circle;
            
            var circDef = evCurveDefinition(context, {
                "edge" : circQuery
            });
            
            // All circles must be on the same plane
            if (i == 0)
            {
                sketchPlane = plane(circDef.coordSystem);
            }
            else
            {
                var thisPlane = plane(circDef.coordSystem);
                if (!coplanarPlanes(sketchPlane, thisPlane))
                {
                    throw regenError("All circles must be on the same plane", ["circles"]);
                }
            }
            
            var center2D = worldToPlane(sketchPlane, circDef.coordSystem.origin);
            var rOriginal = circDef.radius;
            var offsetAmount = circItem.offset;
            
            circles = append(circles, {
                "x" : center2D[0],
                "y" : center2D[1],
                "r" : rOriginal,
                "rOriginal" : rOriginal,
                "offset" : circItem.offset,
                "index" : i
            });
            
            offsetCircles = append(offsetCircles, {
                "x" : center2D[0],
                "y" : center2D[1],
                "r" : rOriginal + offsetAmount,
                "rOriginal" : rOriginal,
                "offset" : circItem.offset,
                "index" : i
            });
        }
        
        // Compute the tangent polygon using offset circles
        var segments = computeTangentPolygon(context, id, sketchPlane, offsetCircles, showDebug);
        
        if (size(segments) == 0)
        {
            throw regenError("Could not compute tangent lines", ["circles"]);
        }
        
        // Create sketch for original circles (reference)
        var sketchOrig = newSketchOnPlane(context, id + "sketchOriginal", {
            "sketchPlane" : sketchPlane
        });
        
        for (var i = 0; i < size(circles); i += 1)
        {
            var circ = circles[i];
            skCircle(sketchOrig, "circleOrig" ~ i, {
                "center" : vector(circ.x, circ.y),
                "radius" : circ.rOriginal
            });
        }
        
        skSolve(sketchOrig);
        
        // Create sketch and draw offset circles and tangent lines
        var sketch = newSketchOnPlane(context, id + "sketch1", {
            "sketchPlane" : sketchPlane
        });
        
        // Draw the offset circles (these are the main circles to use)
        for (var i = 0; i < size(offsetCircles); i += 1)
        {
            var circ = offsetCircles[i];
            skCircle(sketch, "circle" ~ i, {
                "center" : vector(circ.x, circ.y),
                "radius" : circ.r
            });
        }
        
        // Draw the tangent lines
        for (var i = 0; i < size(segments); i += 1)
        {
            var seg = segments[i];
            skLineSegment(sketch, "line" ~ i, {
                "start" : vector(seg.p1.x, seg.p1.y),
                "end" : vector(seg.p2.x, seg.p2.y)
            });
        }
        
        skSolve(sketch);
        
        // If extruding, create a clean outer boundary sketch with arcs only
        if (definition.extrudePlate)
        {
            // Calculate centroid of all circles
            var centroidX = 0 * meter;
            var centroidY = 0 * meter;
            for (var c in offsetCircles)
            {
                centroidX += c.x;
                centroidY += c.y;
            }
            centroidX = centroidX / size(offsetCircles);
            centroidY = centroidY / size(offsetCircles);
            
            // Create offset plane if needed
            var extrudePlane = sketchPlane;
            if (definition.hasOffset)
            {
                var offsetDist = definition.offsetDistance * (definition.flipOffsetDirection ? -1 : 1);
                extrudePlane = plane(sketchPlane.origin + sketchPlane.normal * offsetDist, sketchPlane.normal, sketchPlane.x);
            }
            
            var sketchBoundary = newSketchOnPlane(context, id + "sketchBoundary", {
                "sketchPlane" : extrudePlane
            });
            
            // For each segment, draw the tangent line and the arc on the target circle
            for (var i = 0; i < size(segments); i += 1)
            {
                var seg = segments[i];
                var nextIdx = (i + 1) % size(segments);
                var nextSeg = segments[nextIdx];
                
                // Draw tangent line
                skLineSegment(sketchBoundary, "boundLine" ~ i, {
                    "start" : vector(seg.p1.x, seg.p1.y),
                    "end" : vector(seg.p2.x, seg.p2.y)
                });
                
                // Draw arc on the circle between this segment's p2 and next segment's p1
                var circleIdx = seg.to;
                var circle = offsetCircles[circleIdx];
                
                // The arc goes from seg.p2 to nextSeg.p1 on this circle
                var arcStart = vector(seg.p2.x, seg.p2.y);
                var arcEnd = vector(nextSeg.p1.x, nextSeg.p1.y);
                
                // Calculate a point on the arc for the three-point arc
                var angle1 = atan2(seg.p2.y - circle.y, seg.p2.x - circle.x);
                var angle2 = atan2(nextSeg.p1.y - circle.y, nextSeg.p1.x - circle.x);
                
                // Calculate both possible midpoints (for each arc direction)
                var angleDiff = angle2 - angle1;
                while (angleDiff < 0 * radian) angleDiff += 2 * PI * radian;
                while (angleDiff >= 2 * PI * radian) angleDiff -= 2 * PI * radian;
                
                // Midpoint option 1: shorter arc
                var midAngle1 = angle1 + angleDiff / 2;
                var midPoint1 = vector(
                    circle.x + circle.r * cos(midAngle1),
                    circle.y + circle.r * sin(midAngle1)
                );
                
                // Midpoint option 2: longer arc
                var altAngleDiff = angleDiff - 2 * PI * radian;
                var midAngle2 = angle1 + altAngleDiff / 2;
                var midPoint2 = vector(
                    circle.x + circle.r * cos(midAngle2),
                    circle.y + circle.r * sin(midAngle2)
                );
                
                // Choose the arc whose midpoint is FARTHER from the centroid (outer arc)
                var dist1Sq = (midPoint1[0] - centroidX) * (midPoint1[0] - centroidX) + 
                              (midPoint1[1] - centroidY) * (midPoint1[1] - centroidY);
                var dist2Sq = (midPoint2[0] - centroidX) * (midPoint2[0] - centroidX) + 
                              (midPoint2[1] - centroidY) * (midPoint2[1] - centroidY);
                
                var midPoint = dist1Sq > dist2Sq ? midPoint1 : midPoint2;
                
                skArc(sketchBoundary, "boundArc" ~ i, {
                    "start" : arcStart,
                    "mid" : midPoint,
                    "end" : arcEnd
                });
            }
            
            skSolve(sketchBoundary);
            
            // Extrude the boundary sketch
            // Note: If offset is enabled, the sketch is already on an offset plane
            var boundaryRegions = qSketchRegion(id + "sketchBoundary", false);
            if (size(evaluateQuery(context, boundaryRegions)) > 0)
            {
                extrude(context, id + "extrude1", {
                    "entities" : boundaryRegions,
                    "operationType" : NewBodyOperationType.NEW,
                    "endBound" : BoundingType.BLIND,
                    "oppositeDirection" : definition.flipDirection,
                    "depth" : definition.depth
                });
            }
        }
        
        // Create holes through the plate if "Keep holes" is enabled
        if (definition.keepHoles && definition.extrudePlate)
        {
            var holeEdges = evaluateQuery(context, definition.holeCircles);
            
            if (size(holeEdges) > 0 && !isQueryEmpty(context, definition.holeCircles))
            {
                // Use the same plane as the boundary sketch (which may be offset)
                var holePlane = sketchPlane;
                if (definition.hasOffset)
                {
                    var offsetDist = definition.offsetDistance * (definition.flipOffsetDirection ? -1 : 1);
                    holePlane = plane(sketchPlane.origin + sketchPlane.normal * offsetDist, sketchPlane.normal, sketchPlane.x);
                }
                
                // Create a sketch for the hole circles
                var holeSketch = newSketchOnPlane(context, id + "holeSketch", {
                    "sketchPlane" : holePlane
                });
                
                for (var i = 0; i < size(holeEdges); i += 1)
                {
                    var circDef = evCurveDefinition(context, {
                        "edge" : holeEdges[i]
                    });
                    
                    var center2D = worldToPlane(holePlane, circDef.coordSystem.origin);
                    var radius = circDef.radius;
                    
                    skCircle(holeSketch, "hole" ~ i, {
                        "center" : vector(center2D[0], center2D[1]),
                        "radius" : radius
                    });
                }
                
                skSolve(holeSketch);
                
                // Extrude holes symmetrically in both directions to ensure they cut through
                var holeRegions = qSketchRegion(id + "holeSketch", false);
                if (size(evaluateQuery(context, holeRegions)) > 0)
                {
                    opExtrude(context, id + "holeExtrude", {
                        "entities" : holeRegions,
                        "direction" : holePlane.normal,
                        "endBound" : BoundingType.THROUGH_ALL,
                        "endBoundEntityBody" : qCreatedBy(id + "extrude1", EntityType.BODY),
                        "startBound" : BoundingType.THROUGH_ALL,
                        "startBoundEntityBody" : qCreatedBy(id + "extrude1", EntityType.BODY)
                    });
                    
                    // Boolean subtract the holes from the plate
                    opBoolean(context, id + "booleanHoles", {
                        "tools" : qCreatedBy(id + "holeExtrude", EntityType.BODY),
                        "targets" : qCreatedBy(id + "extrude1", EntityType.BODY),
                        "operationType" : BooleanOperationType.SUBTRACTION
                    });
                }
            }
        }
        
        // Hide/show sketches based on user preferences
        // Always delete the original circles sketch (no need to keep it)
        opDeleteBodies(context, id + "deleteOriginal", {
            "entities" : qCreatedBy(id + "sketchOriginal", EntityType.BODY)
        });
        
        // Delete the hole sketch if it was created
        if (definition.keepHoles && !isQueryEmpty(context, qCreatedBy(id + "holeSketch", EntityType.BODY)))
        {
            opDeleteBodies(context, id + "deleteHoleSketch", {
                "entities" : qCreatedBy(id + "holeSketch", EntityType.BODY)
            });
        }
        
        // Control visibility of construction sketches
        if (definition.extrudePlate)
        {
            // When extruding, the extrusion is the main output
            // Control sketch visibility with "Keep sketches" toggle
            if (definition.keepSketches != true)
            {
                // Delete offset circles and lines sketch
                opDeleteBodies(context, id + "deleteSketch1", {
                    "entities" : qCreatedBy(id + "sketch1", EntityType.BODY)
                });
                // Delete boundary sketch
                opDeleteBodies(context, id + "deleteBoundary", {
                    "entities" : qCreatedBy(id + "sketchBoundary", EntityType.BODY)
                });
            }
            // else: keep both sketch1 and sketchBoundary visible for reference
        }
        // When not extruding, sketch1 is the main output, so keep it visible
    });

    // Helper functions

    function computeTangentPolygon(context is Context, id is Id, sketchPlane is Plane, circles is array, showDebug is boolean) returns array
    {
        if (size(circles) < 2)
            return [];
        
        // Filter out circles that are inside the convex hull of others
        var activeIndices = [];
        for (var i = 0; i < size(circles); i += 1)
        {
            if (!isCircleInside(i, circles))
            {
                activeIndices = append(activeIndices, i);
            }
        }
        
        if (size(activeIndices) < 2)
            return [];
        
        // Order circles by convex hull of their BOUNDARIES (not just centers)
        // For each circle, generate points on its boundary
        var boundaryPoints = [];
        for (var idx in activeIndices)
        {
            var c = circles[idx];
            // Generate 8 points around the circle boundary (every 45 degrees)
            for (var angle = 0; angle < 8; angle += 1)
            {
                var theta = angle * 2 * PI / 8 * radian;
                boundaryPoints = append(boundaryPoints, {
                    "x" : c.x + c.r * cos(theta),
                    "y" : c.y + c.r * sin(theta),
                    "index" : idx
                });
            }
        }
        
        var hull = convexHull(boundaryPoints);
        
        // Extract unique circle indices from hull, maintaining order
        var orderedIndices = [];
        for (var h in hull)
        {
            // Check if this index is already in orderedIndices
            var alreadyAdded = false;
            for (var existing in orderedIndices)
            {
                if (existing == h.index)
                {
                    alreadyAdded = true;
                    break;
                }
            }
            
            if (!alreadyAdded)
            {
                orderedIndices = append(orderedIndices, h.index);
            }
        }
        
        var segments = [];
        
        // Special case: with exactly 2 circles, just use both external tangents directly
        if (size(orderedIndices) == 2)
        {
            var idx1 = orderedIndices[0];
            var idx2 = orderedIndices[1];
            var c1 = circles[idx1];
            var c2 = circles[idx2];
            
            var tangentPairs = getOuterTangent(context, id + "tangent_0", sketchPlane, c1, c2, showDebug);
            if (tangentPairs != undefined && size(tangentPairs) == 2)
            {
                // Add both external tangents
                // First tangent: from circle 1 to circle 2
                segments = append(segments, {
                    "from" : idx1,
                    "to" : idx2,
                    "p1" : tangentPairs[0].p1,
                    "p2" : tangentPairs[0].p2
                });
                // Second tangent: from circle 2 back to circle 1 (reverse direction)
                segments = append(segments, {
                    "from" : idx2,
                    "to" : idx1,
                    "p1" : tangentPairs[1].p2,  // Swap p1 and p2 for reverse direction
                    "p2" : tangentPairs[1].p1
                });
            }
        }
        else
        {
            // For 3+ circles, pick tangents that don't intersect other circles
            // Calculate centroid once for all circles
            var centroid = {
                "x" : 0 * meter,
                "y" : 0 * meter
            };
            for (var c in circles)
            {
                centroid.x += c.x;
                centroid.y += c.y;
            }
            centroid.x = centroid.x / size(circles);
            centroid.y = centroid.y / size(circles);
            
            // For each consecutive pair of circles
            for (var i = 0; i < size(orderedIndices); i += 1)
            {
                var idx1 = orderedIndices[i];
                var idx2 = orderedIndices[(i + 1) % size(orderedIndices)];
                var c1 = circles[idx1];
                var c2 = circles[idx2];
                
                var tangentPairs = getOuterTangent(context, id + ("tangent_" ~ i), sketchPlane, c1, c2, showDebug);
                if (tangentPairs == undefined)
                    continue;
                
                // Try both tangents and pick the best one
                var bestTangent = undefined;
                var bestScore = -1e10;  // Dimensionless score
                
                for (var tangent in tangentPairs)
                {
                    // Count how many circles this tangent intersects (excluding the two it connects)
                    var intersectionCount = 0;
                    var minClearance = 1e10 * meter;
                    
                    for (var j = 0; j < size(circles); j += 1)
                    {
                        if (j == idx1 || j == idx2)
                            continue;
                        
                        var otherCircle = circles[j];
                        var clearance = distanceFromLineSegmentToCircle(tangent.p1, tangent.p2, otherCircle);
                        
                        if (clearance < 0 * meter)
                        {
                            // Line intersects this circle - bad!
                            intersectionCount += 1;
                        }
                        
                        if (clearance < minClearance)
                            minClearance = clearance;
                    }
                    
                    // Calculate distance from centroid as tiebreaker
                    var midpoint = {
                        "x" : (tangent.p1.x + tangent.p2.x) / 2,
                        "y" : (tangent.p1.y + tangent.p2.y) / 2
                    };
                    var dx = midpoint.x - centroid.x;
                    var dy = midpoint.y - centroid.y;
                    var distFromCenter = sqrt(dx * dx + dy * dy);
                    
                    // Scoring: prioritize non-intersecting tangents, then clearance, then distance from centroid
                    // Convert everything to dimensionless for comparison
                    var score = -intersectionCount * 1000 + (minClearance / meter) + (distFromCenter / meter) * 0.1;
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTangent = tangent;
                    }
                }
                
                if (bestTangent != undefined)
                {
                    segments = append(segments, {
                        "from" : idx1,
                        "to" : idx2,
                        "p1" : bestTangent.p1,
                        "p2" : bestTangent.p2
                    });
                }
            }
        }
        
        return segments;
    }

    function getOuterTangent(context is Context, debugId is Id, sketchPlane is Plane, c1 is map, c2 is map, showDebug is boolean)
    {
        var dx = c2.x - c1.x;
        var dy = c2.y - c1.y;
        var dist = sqrt(dx * dx + dy * dy);
        
        if (dist == 0 * meter)
            return undefined;
        
        // Check if external tangent exists
        var radiusDiff = abs(c2.r - c1.r);
        if (radiusDiff > dist)
        {
            // Circles overlap too much, no external tangent
            return undefined;
        }
        
        var tangents = [];
        
        // DEBUG: Print calculation values
        if (showDebug)
        {
            println("=== Tangent Calculation ===");
            println("C1: (" ~ c1.x ~ ", " ~ c1.y ~ ") r=" ~ c1.r);
            println("C2: (" ~ c2.x ~ ", " ~ c2.y ~ ") r=" ~ c2.r);
            println("Distance: " ~ dist);
            println("dx: " ~ dx ~ ", dy: " ~ dy);
        }
        
        // Two external tangents using direct vector method
        // Based on: the tangent line has direction perpendicular to both radii
        // and the angle from center line to each radius is related by sin(β) = (r2-r1)/d
        
        for (var sign in [1, -1])
        {
            // Base angle from C1 to C2
            var theta = atan2(dy, dx);
            
            // For external tangent, calculate the angle offset
            // The key insight: sin(beta) = (r1 - r2) / d
            // This beta is the angle from the center-line to the radius at tangent point
            var sinBeta = (c1.r - c2.r) / dist;
            
            // Clamp for safety
            if (sinBeta > 1) sinBeta = 1;
            if (sinBeta < -1) sinBeta = -1;
            
            var beta = asin(sinBeta);
            
            // The perpendicular offset from center line depends on which tangent (top or bottom)
            // For external tangent: both circles on same side
            // Angle to tangent point on circle 1: theta + sign*(90° - beta)
            // Angle to tangent point on circle 2: theta + sign*(90° - beta)
            var angle1 = theta + sign * (PI / 2 * radian - beta);
            var angle2 = theta + sign * (PI / 2 * radian - beta);
            
            // Calculate tangent points
            var p1 = {
                "x" : c1.x + c1.r * cos(angle1),
                "y" : c1.y + c1.r * sin(angle1)
            };
            
            var p2 = {
                "x" : c2.x + c2.r * cos(angle2),
                "y" : c2.y + c2.r * sin(angle2)
            };
            
            if (showDebug)
            {
                println("Sign: " ~ sign);
                println("  sinBeta: " ~ sinBeta);
                println("  beta: " ~ (beta / radian) ~ " rad = " ~ (beta * 180 / PI / radian) ~ " deg");
                println("  theta (base): " ~ (theta / radian) ~ " rad = " ~ (theta * 180 / PI / radian) ~ " deg");
                println("  angle1: " ~ (angle1 / radian) ~ " rad = " ~ (angle1 * 180 / PI / radian) ~ " deg");
                println("  angle2: " ~ (angle2 / radian) ~ " rad = " ~ (angle2 * 180 / PI / radian) ~ " deg");
                println("  p1: (" ~ p1.x ~ ", " ~ p1.y ~ ")");
                println("  p2: (" ~ p2.x ~ ", " ~ p2.y ~ ")");
                
                // Debug geometry temporarily disabled due to Id concatenation issues
                // The console output above shows all the calculated values
            }
            
            tangents = append(tangents, { "p1" : p1, "p2" : p2, "sign" : sign });
        }
        
        return tangents;
    }

    function cross(o is map, a is map, b is map) returns ValueWithUnits
    {
        return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }

    function convexHull(points is array) returns array
    {
        if (size(points) < 3)
            return points;
        
        // Sort points by x, then y (manual bubble sort since FeatureScript doesn't support custom comparators)
        var sorted = points;
        for (var i = 0; i < size(sorted); i += 1)
        {
            for (var j = i + 1; j < size(sorted); j += 1)
            {
                var needSwap = false;
                if (abs(sorted[i].x - sorted[j].x) < 1e-9 * meter)
                {
                    needSwap = sorted[i].y > sorted[j].y;
                }
                else
                {
                    needSwap = sorted[i].x > sorted[j].x;
                }
                
                if (needSwap)
                {
                    var temp = sorted[i];
                    sorted[i] = sorted[j];
                    sorted[j] = temp;
                }
            }
        }
        
        // Build lower hull
        var lower = [];
        for (var p in sorted)
        {
            while (size(lower) >= 2 && cross(lower[size(lower) - 2], lower[size(lower) - 1], p) <= 0 * meter^2)
            {
                lower = resize(lower, size(lower) - 1);
            }
            lower = append(lower, p);
        }
        
        // Build upper hull
        var upper = [];
        for (var i = size(sorted) - 1; i >= 0; i -= 1)
        {
            var p = sorted[i];
            while (size(upper) >= 2 && cross(upper[size(upper) - 2], upper[size(upper) - 1], p) <= 0 * meter^2)
            {
                upper = resize(upper, size(upper) - 1);
            }
            upper = append(upper, p);
        }
        
        // Remove last point from each half because it's repeated
        lower = resize(lower, size(lower) - 1);
        upper = resize(upper, size(upper) - 1);
        
        return concatenateArrays([lower, upper]);
    }

    function distanceFromLineSegmentToCircle(p1 is map, p2 is map, circle is map) returns ValueWithUnits
    {
        // Calculate the distance from a line segment to the edge of a circle
        // Returns negative if the segment intersects the circle
        
        var dx = p2.x - p1.x;
        var dy = p2.y - p1.y;
        var segLenSq = dx * dx + dy * dy;
        
        if (segLenSq == 0 * meter^2)
        {
            // Degenerate segment (point), use point-to-circle distance
            var distToCenterSq = (p1.x - circle.x) * (p1.x - circle.x) + (p1.y - circle.y) * (p1.y - circle.y);
            return sqrt(distToCenterSq) - circle.r;
        }
        
        // Project circle center onto line segment
        var t = ((circle.x - p1.x) * dx + (circle.y - p1.y) * dy) / segLenSq;
        t = max(0, min(1, t));  // Clamp to segment [0, 1]
        
        // Find closest point on segment to circle center
        var closestX = p1.x + t * dx;
        var closestY = p1.y + t * dy;
        
        // Distance from circle center to closest point on segment
        var distToCenterSq = (circle.x - closestX) * (circle.x - closestX) + (circle.y - closestY) * (circle.y - closestY);
        var distToCenter = sqrt(distToCenterSq);
        
        // Return distance to circle edge (negative if segment intersects circle)
        return distToCenter - circle.r;
    }

    function isCircleInside(circleIdx is number, circles is array) returns boolean
    {
        var circle = circles[circleIdx];
        var otherCircles = [];
        
        for (var i = 0; i < size(circles); i += 1)
        {
            if (i != circleIdx)
            {
                otherCircles = append(otherCircles, circles[i]);
            }
        }
        
        if (size(otherCircles) < 3)
            return false;
        
        // Get convex hull of other circle centers
        var otherCenters = [];
        for (var c in otherCircles)
        {
            otherCenters = append(otherCenters, {
                "x" : c.x,
                "y" : c.y
            });
        }
        
        var hull = convexHull(otherCenters);
        if (size(hull) < 3)
            return false;
        
        // Check if circle center is inside the hull polygon (point in polygon test)
        var centerInside = false;
        for (var i = 0; i < size(hull); i += 1)
        {
            var j = (i + size(hull) - 1) % size(hull);
            var xi = hull[i].x;
            var yi = hull[i].y;
            var xj = hull[j].x;
            var yj = hull[j].y;
            
            var intersect = ((yi > circle.y) != (yj > circle.y)) &&
                (circle.x < (xj - xi) * (circle.y - yi) / (yj - yi) + xi);
            if (intersect)
                centerInside = !centerInside;
        }
        
        // If center is not inside, the circle is definitely not inside
        if (!centerInside)
            return false;
        
        // Center is inside - now check if the ENTIRE circle (center + radius) is inside
        // by checking if the circle extends beyond the hull edges
        // Calculate minimum distance from circle center to any hull edge
        var minDistToHull = 1e10 * meter;
        
        for (var i = 0; i < size(hull); i += 1)
        {
            var j = (i + 1) % size(hull);
            var p1 = hull[i];
            var p2 = hull[j];
            
            // Calculate distance from point to line segment
            var dx = p2.x - p1.x;
            var dy = p2.y - p1.y;
            var segLenSq = dx * dx + dy * dy;
            
            if (segLenSq == 0 * meter^2)
            {
                // Degenerate segment, use point distance
                var distSq = (circle.x - p1.x) * (circle.x - p1.x) + (circle.y - p1.y) * (circle.y - p1.y);
                var dist = sqrt(distSq);
                if (dist < minDistToHull)
                    minDistToHull = dist;
            }
            else
            {
                // Project point onto line segment
                var t = ((circle.x - p1.x) * dx + (circle.y - p1.y) * dy) / segLenSq;
                t = max(0, min(1, t));  // Clamp to segment
                
                var projX = p1.x + t * dx;
                var projY = p1.y + t * dy;
                
                var distSq = (circle.x - projX) * (circle.x - projX) + (circle.y - projY) * (circle.y - projY);
                var dist = sqrt(distSq);
                
                if (dist < minDistToHull)
                    minDistToHull = dist;
            }
        }
        
        // Circle is only truly "inside" if its radius is smaller than the distance to the hull
        // Add a small tolerance to avoid floating point issues
        return (circle.r < minDistToHull - 1e-6 * meter);
    }


