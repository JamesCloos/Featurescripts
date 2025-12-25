FeatureScript 2837;
import(path : "onshape/std/common.fs", version : "2837.0");
import(path : "onshape/std/geometry.fs", version : "2837.0");

/**
 * Motor Standoff Hole FeatureScript
 * Creates an extrude-cut hole at selected points, circles, circular edges, or mate connectors.
 * Supports selectable hole diameters: 1.125", 0.875", or 16mm.
 * Extrudes through all in both directions with optional merge scope.
 * Can add smaller mounting holes around the main hole.
 */

export enum BearingSize
{
    annotation { "Name" : "None" }
    NONE,
    annotation { "Name" : "1.125\"" }
    INCH_1_125,
    annotation { "Name" : "0.875\"" }
    INCH_0_875,
    annotation { "Name" : "16mm" }
    MM_16
}

export enum MountingHoleSize
{
    annotation { "Name" : "10-32" }
    TEN_32,
    annotation { "Name" : "M6" }
    M6
}

export enum MountingHolePlacement
{
    annotation { "Name" : "Symmetric" }
    SYMMETRIC,
    annotation { "Name" : "Select Points" }
    SELECT_POINTS,
    annotation { "Name" : "Manual Input" }
    MANUAL_INPUT
}

export enum HoleCount
{
    annotation { "Name" : "3" }
    THREE,
    annotation { "Name" : "4" }
    FOUR
}

export enum MountingPattern
{
    annotation { "Name" : "CIM-like" }
    CIMLIKE,
    annotation { "Name" : "550" }
    FIVEFIFTY,
    annotation { "Name" : "775" }
    SEVENSEVENFIVE,
    annotation { "Name" : "BAG" }
    BAG,
    annotation { "Name" : "VersaPlanetary" }
    VERSAPLANETARY,
    annotation { "Name" : "Sport" }
    SPORT,
    annotation { "Name" : "UltraPlanetary" }
    ULTRAPLANETARY,
    annotation { "Name" : "MAXPlanetary" }
    MAXPLANETARY,
    annotation { "Name" : "Bearing Hat" }
    BEARINGHAT
}

export enum NumMotorHoles
{
    annotation { "Name" : "Two (standard)" }
    TWO,
    annotation { "Name" : "Four (NEO)" }
    FOUR,
    annotation { "Name" : "Six (UP and Falcon)" }
    SIX,
    annotation { "Name" : "Eight (MAX Planetary)" }
    EIGHT
}

function makePatternDefinition(boltCircle is ValueWithUnits, holeDia is ValueWithUnits, bossSize is ValueWithUnits)
{
    return { "boltCircle" : boltCircle, "holeDia" : holeDia, "bossSize" : bossSize };
}

annotation { 
    "Feature Type Name" : "Motor Standoff Hole",
    "Feature Type Description": "Creates an extrude-cut hole at selected points, circles, circular edges, or mate connectors."
}
export const motorStandoffHole = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { 
            "Name" : "Location", 
            "Description" : "Select a point, circle, circular edge, or mate connector",
            "Filter" : EntityType.VERTEX || (EntityType.EDGE && GeometryType.CIRCLE) || BodyType.MATE_CONNECTOR,
            "MaxNumberOfPicks" : 1 
        }
        definition.location is Query;
        
        annotation { "Name" : "Merge Scope", "Filter" : EntityType.BODY, "MaxNumberOfPicks" : 1 }
        definition.mergeScope is Query;
        
        annotation { "Name" : "BEARING SIZE", "UIHint" : [UIHint.SHOW_LABEL], "Default" : BearingSize.INCH_1_125 }
        definition.bearingSize is BearingSize;
        
        annotation { "Name" : "Add Retaining Screws", "Description" : "1.125\" = 10-32 screws, 0.875\" = M4 screws, 16mm = M3 screws", "Default" : true }
        definition.addRetainingScrews is boolean;
        
        if (definition.addRetainingScrews && definition.bearingSize != BearingSize.NONE)
        {
            annotation { "Name" : "Retaining Screw Angle Offset" }
            isAngle(definition.retainingAngleOffset, ANGLE_360_ZERO_DEFAULT_BOUNDS);
        }
        
        annotation { "Name" : "BOLT SIZE", "UIHint" : [UIHint.SHOW_LABEL] }
        definition.mountingHoleSize is MountingHoleSize;
        
        annotation { "Name" : "Tapped", "Default" : false }
        definition.tapped is boolean;
        
        annotation { "Name" : "STANDOFF TECHNIQUE", "UIHint" : [UIHint.SHOW_LABEL] }
        definition.placementMethod is MountingHolePlacement;
            
            if (definition.placementMethod == MountingHolePlacement.SYMMETRIC)
            {
                annotation { "Name" : "Number of Holes" }
                definition.holeCount is HoleCount;
                
                annotation { "Name" : "Radius from Center" }
                isLength(definition.radius, LENGTH_BOUNDS);
                
                annotation { "Name" : "Angle Offset" }
                isAngle(definition.angleOffset, ANGLE_360_ZERO_DEFAULT_BOUNDS);
            }
            else if (definition.placementMethod == MountingHolePlacement.SELECT_POINTS)
            {
                annotation { "Name" : "Points", "Filter" : EntityType.VERTEX && SketchObject.YES, "MinNumberOfPicks" : 3, "MaxNumberOfPicks" : 6 }
                definition.points is Query;
            }
            else if (definition.placementMethod == MountingHolePlacement.MANUAL_INPUT)
            {
                annotation { "Name" : "Number of Holes" }
                definition.holeCountManual is HoleCount;
                
                annotation { "Name" : "Hole 1 - Radius" }
                isLength(definition.radius1, LENGTH_BOUNDS);
                
                annotation { "Name" : "Hole 1 - Angle" }
                isAngle(definition.angle1, ANGLE_360_ZERO_DEFAULT_BOUNDS);
                
                annotation { "Name" : "Hole 2 - Radius" }
                isLength(definition.radius2, LENGTH_BOUNDS);
                
                annotation { "Name" : "Hole 2 - Angle" }
                isAngle(definition.angle2, ANGLE_360_ZERO_DEFAULT_BOUNDS);
                
                annotation { "Name" : "Hole 3 - Radius" }
                isLength(definition.radius3, LENGTH_BOUNDS);
                
                annotation { "Name" : "Hole 3 - Angle" }
                isAngle(definition.angle3, ANGLE_360_ZERO_DEFAULT_BOUNDS);
                
                if (definition.holeCountManual == HoleCount.FOUR)
                {
                    annotation { "Name" : "Hole 4 - Radius" }
                    isLength(definition.radius4, LENGTH_BOUNDS);
                    
                    annotation { "Name" : "Hole 4 - Angle" }
                    isAngle(definition.angle4, ANGLE_360_ZERO_DEFAULT_BOUNDS);
                }
            }
            
            // Align to Geometry - only shown for SYMMETRIC and MANUAL_INPUT
            if (definition.placementMethod == MountingHolePlacement.SYMMETRIC || definition.placementMethod == MountingHolePlacement.MANUAL_INPUT)
            {
                annotation { "Name" : "Align to Geometry", "Default" : false }
                definition.alignToGeometry is boolean;
                
                if (definition.alignToGeometry)
                {
                    annotation { "Name" : "Angle Reference", "Filter" : EntityType.EDGE || EntityType.FACE || QueryFilterCompound.ALLOWS_PLANE, "MaxNumberOfPicks" : 1 }
                    definition.angleReference is Query;
                }
            }
        
        annotation { "Name" : "Spacer Length" }
        isLength(definition.spacerLength, { (inch) : [0.125, 1.0, 10] } as LengthBoundSpec);
        
        annotation { "Name" : "Add Plate", "Default" : false }
        definition.addPlate is boolean;
        
        if (definition.addPlate)
        {
            annotation { "Name" : "No Plate Overhang", "Default" : false }
            definition.noPlateOverhang is boolean;
            
            annotation { "Name" : "Motor Pattern" }
            definition.motorPattern is MountingPattern;
            
            annotation { "Name" : "Plate Thickness" }
            isLength(definition.plateThickness, { (inch) : [0.0625, 0.125, 1.0] } as LengthBoundSpec);
            
            annotation { "Name" : "NUMBER OF HOLES", "UIHint" : [UIHint.SHOW_LABEL] }
            definition.numMotorHoles is NumMotorHoles;
            
            annotation { "Name" : "Override Boss Size" }
            definition.overrideBoss is boolean;
            
            if (definition.overrideBoss)
            {
                annotation { "Name" : "Boss Diameter" }
                isLength(definition.bossDia, { (inch) : [0.0, 1.125, 4] } as LengthBoundSpec);
            }
        }
    }
    {
        // Validate location selection
        if (isQueryEmpty(context, definition.location))
        {
            throw regenError("Please select a point, circle, circular edge, or mate connector.", ["location"]);
        }
        
        // Get hole diameter based on selection
        var holeRadius;
        var createMainHole = true;
        if (definition.bearingSize == BearingSize.NONE)
        {
            createMainHole = false;
            holeRadius = 0 * inch; // Placeholder, won't be used
        }
        else if (definition.bearingSize == BearingSize.INCH_1_125)
        {
            holeRadius = 1.125 * inch / 2;
        }
        else if (definition.bearingSize == BearingSize.INCH_0_875)
        {
            holeRadius = 0.875 * inch / 2;
        }
        else // MM_16
        {
            holeRadius = 16 * millimeter / 2;
        }
        
        // Get mounting hole radius and spacer inner diameter
        var mountingHoleRadius;
        var spacerInnerRadius; // Always use clearance hole size for spacer ID
        
        if (definition.mountingHoleSize == MountingHoleSize.TEN_32)
        {
            spacerInnerRadius = 5 * millimeter / 2;
            if (definition.tapped)
            {
                mountingHoleRadius = 4.3 * millimeter / 2;
            }
            else
            {
                mountingHoleRadius = spacerInnerRadius;
            }
        }
        else // M6
        {
            spacerInnerRadius = 6 * millimeter / 2;
            if (definition.tapped)
            {
                mountingHoleRadius = 5 * millimeter / 2;
            }
            else
            {
                mountingHoleRadius = spacerInnerRadius;
            }
        }
        
        // Determine the location and plane from the selected entity
        var centerPoint;
        var sketchPlane;
        
        // Check if it's a mate connector
        const isMateConnector = !isQueryEmpty(context, qBodyType(definition.location, BodyType.MATE_CONNECTOR));
        
        if (isMateConnector)
        {
            // It's a mate connector
            var mateConnector = evMateConnector(context, {"mateConnector" : definition.location});
            centerPoint = mateConnector.origin;
            sketchPlane = plane(mateConnector);
        }
        else
        {
            // Check if it's a vertex
            const isVertex = !isQueryEmpty(context, qEntityFilter(definition.location, EntityType.VERTEX));
            
            if (isVertex)
            {
                // It's a vertex/point
                centerPoint = evVertexPoint(context, {"vertex" : definition.location});
                
                // Try to get the sketch plane from the vertex
                try
                {
                    sketchPlane = evOwnerSketchPlane(context, {"entity" : definition.location});
                }
                catch
                {
                    // If no sketch plane, we need to find an adjacent face or use a default plane
                    // Try to find an adjacent face
                    var adjacentFaces = qAdjacent(definition.location, AdjacencyType.VERTEX, EntityType.FACE);
                    if (!isQueryEmpty(context, adjacentFaces))
                    {
                        var face = qNthElement(adjacentFaces, 0);
                        try
                        {
                            sketchPlane = evPlane(context, {"face" : face});
                        }
                        catch
                        {
                            // If face is not planar, use a default plane (XY plane at the point)
                            sketchPlane = plane(centerPoint, vector(0, 0, 1));
                        }
                    }
                    else
                    {
                        // Use a default plane (XY plane at the point)
                        sketchPlane = plane(centerPoint, vector(0, 0, 1));
                    }
                }
            }
            else
            {
                // It's a circle or circular edge
                var curveData = evCurveDefinition(context, {"edge" : definition.location});
                if (curveData is Circle)
                {
                    centerPoint = curveData.coordSystem.origin;
                    
                    // Try to get the plane from the sketch
                    try
                    {
                        sketchPlane = evOwnerSketchPlane(context, {"entity" : definition.location});
                    }
                    catch
                    {
                        // If not a sketch entity, get the plane from the circle's coordinate system
                        sketchPlane = plane(curveData.coordSystem);
                    }
                }
                else
                {
                    throw regenError("Selected edge is not a circle. Please select a point, circle, circular edge, or mate connector.", ["location"]);
                }
            }
        }
        
        // Create a sketch on the determined plane
        var sketch = newSketchOnPlane(context, id + "sketch", {
            "sketchPlane" : sketchPlane
        });
        
        // Convert 3D center point to 2D sketch coordinates
        var sketchCenter = worldToPlane(sketchPlane, centerPoint);
        
        // Draw the main circle in the sketch (if not "None")
        if (createMainHole)
        {
            skCircle(sketch, "holeCircle", {
                "center" : sketchCenter,
                "radius" : holeRadius
            });
            
            // Add retaining screws if enabled
            if (definition.addRetainingScrews)
            {
                var retainingScrewRadius;
                var retainingScrewDistance;
                
                if (definition.bearingSize == BearingSize.INCH_1_125)
                {
                    retainingScrewRadius = 4.3 * millimeter / 2;
                    retainingScrewDistance = 18.06 * millimeter;
                }
                else if (definition.bearingSize == BearingSize.INCH_0_875)
                {
                    retainingScrewRadius = 3.6 * millimeter / 2;
                    retainingScrewDistance = 14.3 * millimeter;
                }
                else // MM_16
                {
                    retainingScrewRadius = 2.7 * millimeter / 2;
                    retainingScrewDistance = 10.5 * millimeter;
                }
                
                // Create two holes opposite each other
                var retainingAngle = definition.retainingAngleOffset;
                
                // First retaining screw
                var retaining1Pos = sketchCenter + vector(
                    retainingScrewDistance * cos(retainingAngle),
                    retainingScrewDistance * sin(retainingAngle)
                );
                skCircle(sketch, "retainingScrew1", {
                    "center" : retaining1Pos,
                    "radius" : retainingScrewRadius
                });
                
                // Second retaining screw (opposite side)
                var retaining2Pos = sketchCenter + vector(
                    retainingScrewDistance * cos(retainingAngle + 180 * degree),
                    retainingScrewDistance * sin(retainingAngle + 180 * degree)
                );
                skCircle(sketch, "retainingScrew2", {
                    "center" : retaining2Pos,
                    "radius" : retainingScrewRadius
                });
            }
        }
        
        // Add mounting holes
        var mountingHolePositions = [];
        var mountingHoleAbsolutePositions = []; // Store absolute positions for spacer creation later
        var useAbsolutePositions = false; // Flag to track if positions are absolute or relative
        
        // Calculate angle offset from reference geometry if provided
        var geometryAngleOffset = 0 * degree;
        if ((definition.placementMethod == MountingHolePlacement.SYMMETRIC || definition.placementMethod == MountingHolePlacement.MANUAL_INPUT) &&
            definition.alignToGeometry && !isQueryEmpty(context, definition.angleReference))
        {
            try silent
            {
                // Try to get a line from an edge
                var line is Line = evLine(context, {
                    "edge" : definition.angleReference
                });
                var lineDir = line.direction;
                var xdir = sketchPlane.x;
                geometryAngleOffset = angleBetween(lineDir, xdir);
                if (dot(cross(lineDir, xdir), sketchPlane.normal) > 0)
                {
                    geometryAngleOffset *= -1;
                }
            }
            catch
            {
                try silent
                {
                    // Try to get a plane from a face
                    var facePlane is Plane = evPlane(context, {
                        "face" : definition.angleReference
                    });
                    var planeXDir = facePlane.x;
                    var xdir = sketchPlane.x;
                    geometryAngleOffset = angleBetween(planeXDir, xdir);
                    if (dot(cross(planeXDir, xdir), sketchPlane.normal) > 0)
                    {
                        geometryAngleOffset *= -1;
                    }
                }
            }
        }
            
            if (definition.placementMethod == MountingHolePlacement.SYMMETRIC)
            {
                // Create symmetric circular pattern
                var numHoles = (definition.holeCount == HoleCount.THREE) ? 3 : 4;
                var radius = definition.radius;
                var angleOffset = definition.angleOffset + geometryAngleOffset;
                
                for (var i = 0; i < numHoles; i += 1)
                {
                    var angle = angleOffset + (i * 360 / numHoles) * degree;
                    var x = radius * cos(angle);
                    var y = radius * sin(angle);
                    mountingHolePositions = append(mountingHolePositions, vector(x, y));
                }
            }
            else if (definition.placementMethod == MountingHolePlacement.SELECT_POINTS)
            {
                // Use selected points - these are absolute positions in sketch plane
                useAbsolutePositions = true;
                var pointsArray = evaluateQuery(context, definition.points);
                for (var point in pointsArray)
                {
                    var point3D = evVertexPoint(context, {"vertex" : point});
                    var point2D = worldToPlane(sketchPlane, point3D);
                    mountingHolePositions = append(mountingHolePositions, point2D);
                }
            }
            else if (definition.placementMethod == MountingHolePlacement.MANUAL_INPUT)
            {
                // Manual input positions
                var numHoles = (definition.holeCountManual == HoleCount.THREE) ? 3 : 4;
                
                var r1 = definition.radius1;
                var a1 = definition.angle1 + geometryAngleOffset;
                mountingHolePositions = append(mountingHolePositions, vector(r1 * cos(a1), r1 * sin(a1)));
                
                var r2 = definition.radius2;
                var a2 = definition.angle2 + geometryAngleOffset;
                mountingHolePositions = append(mountingHolePositions, vector(r2 * cos(a2), r2 * sin(a2)));
                
                var r3 = definition.radius3;
                var a3 = definition.angle3 + geometryAngleOffset;
                mountingHolePositions = append(mountingHolePositions, vector(r3 * cos(a3), r3 * sin(a3)));
                
                if (numHoles == 4)
                {
                    var r4 = definition.radius4;
                    var a4 = definition.angle4 + geometryAngleOffset;
                    mountingHolePositions = append(mountingHolePositions, vector(r4 * cos(a4), r4 * sin(a4)));
                }
            }
            
            // Draw mounting holes at calculated positions
            for (var i = 0; i < size(mountingHolePositions); i += 1)
            {
                var pos = mountingHolePositions[i];
                var holeCenter;
                
                if (useAbsolutePositions)
                {
                    // Position is already absolute in sketch coordinates
                    holeCenter = pos;
                }
                else
                {
                    // Position is relative to the main hole center
                    holeCenter = sketchCenter + pos;
                }
                
                // Store absolute position for spacer creation
                mountingHoleAbsolutePositions = append(mountingHoleAbsolutePositions, holeCenter);
                
                skCircle(sketch, "mountingHole" ~ i, {
                    "center" : holeCenter,
                    "radius" : mountingHoleRadius
                });
            }
        
        skSolve(sketch);
        
        // Get sketch regions for extrusion
        var sketchRegions = qSketchRegion(id + "sketch", true);
        
        // Check if merge scope is provided
        var useMergeScope = !isQueryEmpty(context, definition.mergeScope);
        
        // Extrude-cut through all in both directions
        // First direction
        if (useMergeScope)
        {
            extrude(context, id + "extrude1", {
                "entities" : sketchRegions,
                "endBound" : BoundingType.THROUGH_ALL,
                "operationType" : NewBodyOperationType.REMOVE,
                "defaultScope" : false,
                "booleanScope" : definition.mergeScope
            });
        }
        else
        {
            extrude(context, id + "extrude1", {
                "entities" : sketchRegions,
                "endBound" : BoundingType.THROUGH_ALL,
                "operationType" : NewBodyOperationType.REMOVE
            });
        }
        
        // Second direction (opposite)
        if (useMergeScope)
        {
            extrude(context, id + "extrude2", {
                "entities" : sketchRegions,
                "endBound" : BoundingType.THROUGH_ALL,
                "oppositeDirection" : true,
                "operationType" : NewBodyOperationType.REMOVE,
                "defaultScope" : false,
                "booleanScope" : definition.mergeScope
            });
        }
        else
        {
            extrude(context, id + "extrude2", {
                "entities" : sketchRegions,
                "endBound" : BoundingType.THROUGH_ALL,
                "oppositeDirection" : true,
                "operationType" : NewBodyOperationType.REMOVE
            });
        }
        
        // Delete the sketch body after extrusion
        opDeleteBodies(context, id + "deleteSketch", {
            "entities" : qCreatedBy(id + "sketch", EntityType.BODY)
        });
        
        // Define spacer dimensions (used by both spacer and plate generation)
        var spacerOuterRadius = 0.375 * inch / 2; // Fixed 3/8" OD
        
        // Create spacers
        if (size(mountingHoleAbsolutePositions) > 0)
        {
            // Create a sketch for the spacers on the same plane
            var spacerSketch = newSketchOnPlane(context, id + "spacerSketch", {
                "sketchPlane" : sketchPlane
            });
            
            // Draw rings at each mounting hole position
            for (var i = 0; i < size(mountingHoleAbsolutePositions); i += 1)
            {
                var center = mountingHoleAbsolutePositions[i];
                
                // Draw outer circle
                skCircle(spacerSketch, "spacerOuter" ~ i, {
                    "center" : center,
                    "radius" : spacerOuterRadius
                });
                
                // Draw inner hole
                skCircle(spacerSketch, "spacerInner" ~ i, {
                    "center" : center,
                    "radius" : spacerInnerRadius
                });
            }
            
            skSolve(spacerSketch);
            
            // Extrude spacers in the direction of the sketch normal (away from body)
            var spacerRegions = qSketchRegion(id + "spacerSketch", true);
            
            opExtrude(context, id + "extrudeSpacers", {
                "entities" : spacerRegions,
                "direction" : sketchPlane.normal,
                "endBound" : BoundingType.BLIND,
                "endDepth" : definition.spacerLength
            });
            
            // Set properties for all spacer bodies
            var spacerBodies = qCreatedBy(id + "extrudeSpacers", EntityType.BODY);
            var lengthInInches = definition.spacerLength / inch;
            var lengthFormatted = toString(roundToPrecision(lengthInInches, 3));
            var spacerName = lengthFormatted ~ " Inch Spacer";
            
            for (var spacerBody in evaluateQuery(context, spacerBodies))
            {
                // Set name
                setProperty(context, {
                    "entities" : spacerBody,
                    "propertyType" : PropertyType.NAME,
                    "value" : spacerName
                });
                
                // Set color to dark gray
                setProperty(context, {
                    "entities" : spacerBody,
                    "propertyType" : PropertyType.APPEARANCE,
                    "value" : color(0.3, 0.3, 0.3)
                });
            }
            
            // Delete the spacer sketch body
            opDeleteBodies(context, id + "deleteSpacerSketch", {
                "entities" : qCreatedBy(id + "spacerSketch", EntityType.BODY)
            });
        }
        
        // Create plate if requested
        if (definition.addPlate && size(mountingHoleAbsolutePositions) > 0)
        {
            // Get motor pattern definition
            var mm = millimeter;
            var myPattern = {
                MountingPattern.CIMLIKE : makePatternDefinition(2 * inch, 0.196 * inch, 0.75 * inch),
                MountingPattern.FIVEFIFTY : makePatternDefinition(25 * mm, 3.4 * mm, 13 * mm),
                MountingPattern.SEVENSEVENFIVE : makePatternDefinition(29 * mm, 4.5 * mm, 17.5 * mm),
                MountingPattern.BAG : makePatternDefinition(25 * mm, 4.5 * mm, 12 * mm),
                MountingPattern.VERSAPLANETARY : makePatternDefinition(2 * inch, .1695 * inch, .75 * inch),
                MountingPattern.SPORT : makePatternDefinition(2 * inch, .196 * inch, 1.5 * inch),
                MountingPattern.ULTRAPLANETARY : makePatternDefinition(32 * mm, 3.4 * mm, 22 * mm),
                MountingPattern.MAXPLANETARY : makePatternDefinition(2 * inch, 0.196 * inch, 1.125 * inch),
                MountingPattern.BEARINGHAT : makePatternDefinition(2 * inch, 0.196 * inch, 1.125 * inch)
            }[definition.motorPattern];
            
            var boltCircleDia = myPattern.boltCircle;
            var motorHoleDia = myPattern.holeDia;
            var bossDia;
            if (definition.overrideBoss)
            {
                bossDia = definition.bossDia;
            }
            else
            {
                bossDia = myPattern.bossSize;
            }
            
            // Calculate center circle diameter as bolt circle * 1.125
            var centerCircleDiameter = boltCircleDia * 1.21875;
            var centerCircleRadius = centerCircleDiameter / 2;
            
            // Create plane at the end of the spacers
            var platePlane = plane(sketchPlane.origin + sketchPlane.normal * definition.spacerLength, sketchPlane.normal, sketchPlane.x);
            
            // Build circle arrays for tangent connect algorithm
            var circles = []; // Original circles for holes
            var offsetCircles = []; // Offset circles for boundary
            
            // Add mounting hole circles
            for (var pos in mountingHoleAbsolutePositions)
            {
                circles = append(circles, {
                    "x" : pos[0],
                    "y" : pos[1],
                    "r" : spacerInnerRadius
                });
                
                // Determine plate boundary based on user selection
                var plateRadius;
                if (definition.noPlateOverhang)
                {
                    // Plate boundary exactly at spacer OD (aligns with standoffs)
                    plateRadius = spacerOuterRadius;
                }
                else
                {
                    // Plate edge is 0.125" away from hole edge
                    plateRadius = spacerInnerRadius + 0.125 * inch;
                }
                
                offsetCircles = append(offsetCircles, {
                    "x" : pos[0],
                    "y" : pos[1],
                    "r" : plateRadius
                });
            }
            
            // Add center circle (calculated from motor pattern)
            circles = append(circles, {
                "x" : sketchCenter[0],
                "y" : sketchCenter[1],
                "r" : centerCircleRadius
            });
            
            // Determine center circle plate boundary based on user selection
            var centerPlateRadius;
            if (definition.noPlateOverhang)
            {
                // No overhang - use center circle radius as-is
                centerPlateRadius = centerCircleRadius;
            }
            else
            {
                // Add 0.125" spacing around center circle
                centerPlateRadius = centerCircleRadius + 0.125 * inch;
            }
            
            offsetCircles = append(offsetCircles, {
                "x" : sketchCenter[0],
                "y" : sketchCenter[1],
                "r" : centerPlateRadius
            });
            
            // Compute tangent polygon using OFFSET circles for boundary
            var segments = computeTangentPolygon(offsetCircles);
            
            if (size(segments) > 0)
            {
                // Create sketch on the plate plane
                var plateSketch = newSketchOnPlane(context, id + "plateSketch", {
                    "sketchPlane" : platePlane
                });
                
                // Calculate centroid for arc direction
                var centroidX = 0 * meter;
                var centroidY = 0 * meter;
                for (var c in offsetCircles)
                {
                    centroidX += c.x;
                    centroidY += c.y;
                }
                centroidX = centroidX / size(offsetCircles);
                centroidY = centroidY / size(offsetCircles);
                
                // Draw boundary (tangent lines and arcs) using OFFSET circles
                for (var i = 0; i < size(segments); i += 1)
                {
                    var seg = segments[i];
                    var nextIdx = (i + 1) % size(segments);
                    var nextSeg = segments[nextIdx];
                    
                    // Draw tangent line
                    skLineSegment(plateSketch, "boundLine" ~ i, {
                        "start" : vector(seg.p1.x, seg.p1.y),
                        "end" : vector(seg.p2.x, seg.p2.y)
                    });
                    
                    // Draw arc on the OFFSET circle between this segment's p2 and next segment's p1
                    var circleIdx = seg.to;
                    var circle = offsetCircles[circleIdx];
                    
                    var arcStart = vector(seg.p2.x, seg.p2.y);
                    var arcEnd = vector(nextSeg.p1.x, nextSeg.p1.y);
                    
                    // Calculate arc midpoint
                    var angle1 = atan2(seg.p2.y - circle.y, seg.p2.x - circle.x);
                    var angle2 = atan2(nextSeg.p1.y - circle.y, nextSeg.p1.x - circle.x);
                    
                    var angleDiff = angle2 - angle1;
                    while (angleDiff < 0 * radian) angleDiff += 2 * PI * radian;
                    while (angleDiff >= 2 * PI * radian) angleDiff -= 2 * PI * radian;
                    
                    // Choose outer arc (farther from centroid)
                    var midAngle1 = angle1 + angleDiff / 2;
                    var midPoint1 = vector(
                        circle.x + circle.r * cos(midAngle1),
                        circle.y + circle.r * sin(midAngle1)
                    );
                    
                    var altAngleDiff = angleDiff - 2 * PI * radian;
                    var midAngle2 = angle1 + altAngleDiff / 2;
                    var midPoint2 = vector(
                        circle.x + circle.r * cos(midAngle2),
                        circle.y + circle.r * sin(midAngle2)
                    );
                    
                    var dist1Sq = (midPoint1[0] - centroidX) * (midPoint1[0] - centroidX) + 
                                  (midPoint1[1] - centroidY) * (midPoint1[1] - centroidY);
                    var dist2Sq = (midPoint2[0] - centroidX) * (midPoint2[0] - centroidX) + 
                                  (midPoint2[1] - centroidY) * (midPoint2[1] - centroidY);
                    
                    var midPoint = dist1Sq > dist2Sq ? midPoint1 : midPoint2;
                    
                    skArc(plateSketch, "boundArc" ~ i, {
                        "start" : arcStart,
                        "mid" : midPoint,
                        "end" : arcEnd
                    });
                }
                
                // Add center circle to the same sketch to ensure it's always included
                skCircle(plateSketch, "centerCircleInPlate", {
                    "center" : vector(sketchCenter[0], sketchCenter[1]),
                    "radius" : centerCircleRadius
                });
                
                skSolve(plateSketch);
                
                // Extrude plate - this will include both boundary and center circle
                var plateThickness = definition.plateThickness;
                opExtrude(context, id + "extrudePlate", {
                    "entities" : qSketchRegion(id + "plateSketch", false),
                    "direction" : platePlane.normal,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : plateThickness
                });
                
                // Create separate sketch for holes (on same plane)
                var holeSketch = newSketchOnPlane(context, id + "holeSketch", {
                    "sketchPlane" : platePlane
                });
                
                // Draw hole circles using ORIGINAL circle sizes (not offset)
                // Only cut the mounting holes, NOT the center circle
                for (var i = 0; i < size(mountingHoleAbsolutePositions); i += 1)
                {
                    var pos = mountingHoleAbsolutePositions[i];
                    skCircle(holeSketch, "plateHole" ~ i, {
                        "center" : vector(pos[0], pos[1]),
                        "radius" : spacerInnerRadius
                    });
                }
                
                skSolve(holeSketch);
                
                // Cut holes through the plate
                var holeRegions = qSketchRegion(id + "holeSketch", false);
                if (size(evaluateQuery(context, holeRegions)) > 0)
                {
                    extrude(context, id + "holeExtrude", {
                        "entities" : holeRegions,
                        "endBound" : BoundingType.THROUGH_ALL,
                        "startBound" : BoundingType.THROUGH_ALL,
                        "operationType" : NewBodyOperationType.REMOVE,
                        "defaultScope" : false,
                        "booleanScope" : qCreatedBy(id + "extrudePlate", EntityType.BODY)
                    });
                }
                
                // Determine number of motor holes
                var numMotorHoles;
                if (definition.numMotorHoles == NumMotorHoles.TWO)
                {
                    numMotorHoles = 2;
                }
                else if (definition.numMotorHoles == NumMotorHoles.FOUR)
                {
                    numMotorHoles = 4;
                }
                else if (definition.numMotorHoles == NumMotorHoles.SIX)
                {
                    numMotorHoles = 6;
                }
                else if (definition.numMotorHoles == NumMotorHoles.EIGHT)
                {
                    numMotorHoles = 8;
                }
                
                // Add motor mounting holes
                var motorSketch = newSketchOnPlane(context, id + "motorSketch", {
                    "sketchPlane" : platePlane
                });
                
                // Boss hole at center
                skCircle(motorSketch, "boss", {
                    "center" : vector(sketchCenter[0], sketchCenter[1]),
                    "radius" : bossDia / 2
                });
                
                // Motor mounting holes at bolt circle
                var d = boltCircleDia / 2;
                for (var i = 0; i < numMotorHoles; i += 1)
                {
                    var angle = (i * 360 / numMotorHoles) * degree;
                    var holePos = vector(
                        sketchCenter[0] + d * cos(angle),
                        sketchCenter[1] + d * sin(angle)
                    );
                    skCircle(motorSketch, "motorHole" ~ i, {
                        "center" : holePos,
                        "radius" : motorHoleDia / 2
                    });
                }
                
                skSolve(motorSketch);
                
                // Cut all motor holes through plate
                extrude(context, id + "motorExtrude", {
                    "entities" : qSketchRegion(id + "motorSketch", false),
                    "endBound" : BoundingType.THROUGH_ALL,
                    "startBound" : BoundingType.THROUGH_ALL,
                    "operationType" : NewBodyOperationType.REMOVE,
                    "defaultScope" : false,
                    "booleanScope" : qCreatedBy(id + "extrudePlate", EntityType.BODY)
                });
                
                // Delete sketch bodies
                opDeleteBodies(context, id + "deletePlateSketch", {
                    "entities" : qCreatedBy(id + "plateSketch", EntityType.BODY)
                });
                opDeleteBodies(context, id + "deleteHoleSketch", {
                    "entities" : qCreatedBy(id + "holeSketch", EntityType.BODY)
                });
                opDeleteBodies(context, id + "deleteMotorSketch", {
                    "entities" : qCreatedBy(id + "motorSketch", EntityType.BODY)
                });
            }
        }
    });

// Helper functions for tangent connect algorithm

function computeTangentPolygon(circles is array) returns array
{
    if (size(circles) < 2)
        return [];
    
    // Filter out circles that are inside others
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
    
    // Order circles by convex hull
    var centers = [];
    for (var idx in activeIndices)
    {
        var c = circles[idx];
        centers = append(centers, {
            "x" : c.x,
            "y" : c.y,
            "index" : idx
        });
    }
    
    var hull = convexHull(centers);
    var orderedIndices = [];
    for (var h in hull)
    {
        orderedIndices = append(orderedIndices, h.index);
    }
    
    var segments = [];
    
    // Special case: 2 circles
    if (size(orderedIndices) == 2)
    {
        var idx1 = orderedIndices[0];
        var idx2 = orderedIndices[1];
        var c1 = circles[idx1];
        var c2 = circles[idx2];
        
        var tangentPairs = getOuterTangent(c1, c2);
        if (tangentPairs != undefined && size(tangentPairs) == 2)
        {
            segments = append(segments, {
                "from" : idx1,
                "to" : idx2,
                "p1" : tangentPairs[0].p1,
                "p2" : tangentPairs[0].p2
            });
            segments = append(segments, {
                "from" : idx2,
                "to" : idx1,
                "p1" : tangentPairs[1].p2,
                "p2" : tangentPairs[1].p1
            });
        }
    }
    else
    {
        // 3+ circles
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
        
        for (var i = 0; i < size(orderedIndices); i += 1)
        {
            var idx1 = orderedIndices[i];
            var idx2 = orderedIndices[(i + 1) % size(orderedIndices)];
            var c1 = circles[idx1];
            var c2 = circles[idx2];
            
            var tangentPairs = getOuterTangent(c1, c2);
            if (tangentPairs == undefined)
                continue;
            
            var bestTangent = undefined;
            var bestScore = -1e10 * meter;
            
            for (var tangent in tangentPairs)
            {
                var midpoint = {
                    "x" : (tangent.p1.x + tangent.p2.x) / 2,
                    "y" : (tangent.p1.y + tangent.p2.y) / 2
                };
                
                var dx = midpoint.x - centroid.x;
                var dy = midpoint.y - centroid.y;
                var distFromCenter = sqrt(dx * dx + dy * dy);
                
                if (distFromCenter > bestScore)
                {
                    bestScore = distFromCenter;
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

function getOuterTangent(c1 is map, c2 is map)
{
    var dx = c2.x - c1.x;
    var dy = c2.y - c1.y;
    var dist = sqrt(dx * dx + dy * dy);
    
    if (dist == 0 * meter)
        return undefined;
    
    var radiusDiff = abs(c2.r - c1.r);
    if (radiusDiff > dist)
        return undefined;
    
    var tangents = [];
    
    for (var sign in [1, -1])
    {
        var theta = atan2(dy, dx);
        var sinBeta = (c1.r - c2.r) / dist;
        
        if (sinBeta > 1) sinBeta = 1;
        if (sinBeta < -1) sinBeta = -1;
        
        var beta = asin(sinBeta);
        var angle1 = theta + sign * (PI / 2 * radian - beta);
        var angle2 = theta + sign * (PI / 2 * radian - beta);
        
        var p1 = {
            "x" : c1.x + c1.r * cos(angle1),
            "y" : c1.y + c1.r * sin(angle1)
        };
        
        var p2 = {
            "x" : c2.x + c2.r * cos(angle2),
            "y" : c2.y + c2.r * sin(angle2)
        };
        
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
    
    // Sort points
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
    
    lower = resize(lower, size(lower) - 1);
    upper = resize(upper, size(upper) - 1);
    
    return concatenateArrays([lower, upper]);
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
    
    // Point in polygon test
    var inside = false;
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
            inside = !inside;
    }
    
    return inside;
}

