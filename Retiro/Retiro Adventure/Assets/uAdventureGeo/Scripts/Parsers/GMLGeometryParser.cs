﻿using System.Collections.Generic;
using System.Xml;
using uAdventure.Core;
using UnityEngine;

namespace uAdventure.Geo
{
    [DOMParser("geometry")]
    [DOMParser(typeof(GMLGeometry))]
    public class GMLGeometryParser : IDOMParser
    {
        public object DOMParse(XmlElement element, params object[] parameters)
        {
            var gmlNode = element.FirstChild;

            var geometry = new GMLGeometry();
            geometry.Influence = float.Parse(element.GetAttribute("influence"));
            XmlNode pointsNode;
            switch (gmlNode.Name)
            {
                default: // case "Point":
                    geometry.Type = GMLGeometry.GeometryType.Point;
                    pointsNode = gmlNode.FirstChild;
                    break;
                case "LineString":
                    geometry.Type = GMLGeometry.GeometryType.LineString;
                    pointsNode = gmlNode.FirstChild;
                    break;
                case "Polygon":
                    geometry.Type = GMLGeometry.GeometryType.Polygon;
                    pointsNode = gmlNode.FirstChild.FirstChild.FirstChild; // Polygon -> external -> Ring -> points
                    break;
            }

            geometry.Points = UnzipPoints(pointsNode.InnerText);
            return geometry;
        }

        private Vector2d[] UnzipPoints(string pointList)
        {
            var points = new List<Vector2d>();
            var zippedPoints = pointList.Split(' ');
            for (int i = 0; i < zippedPoints.Length; i += 2)
            {
                points.Add(new Vector2d(double.Parse(zippedPoints[i]), double.Parse(zippedPoints[i + 1])));
            }

            return points.ToArray();
        }
    }
}
