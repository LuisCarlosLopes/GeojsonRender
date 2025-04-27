using System;
using System.Collections.Generic;
using NUnit.Framework;
using GeoJsonRenderer.Domain.Models;
using NetTopologySuite.Geometries;

namespace GeoJsonRenderer.Tests
{
    [TestFixture]
    public class GeoFilterTests
    {
        [Test]
        public void ConstructWithNoParameters_ReturnsFilterWithNoRestrictions()
        {
            // Arrange & Act
            var filter = new GeoFilter();
            
            // Assert
            Assert.That(filter.Conditions, Is.Not.Null);
            Assert.That(filter.Conditions.Count, Is.EqualTo(0));
        }
        
        [Test]
        public void Match_NoFilters_ReturnsTrue()
        {
            // Arrange
            var filter = new GeoFilter();
            var point = new Point(new Coordinate(0, 0));
            var feature = new GeoFeature
            {
                Geometry = point,
                Properties = new Dictionary<string, object>
                {
                    { "name", "test" }
                }
            };
            
            // Act
            bool result = filter.Match(feature);
            
            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void Match_WithSingleCondition_MatchesCorrectly()
        {
            // Arrange
            var filter = new GeoFilter();
            filter.Conditions.Add(new FilterCondition { Property = "name", Value = "test" });
            var point = new Point(new Coordinate(0, 0));
            var feature = new GeoFeature
            {
                Geometry = point,
                Properties = new Dictionary<string, object>
                {
                    { "name", "test" }
                }
            };
            
            // Act
            bool result = filter.Match(feature);
            
            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void Match_WithSingleCondition_DoesNotMatch()
        {
            // Arrange
            var filter = new GeoFilter();
            filter.Conditions.Add(new FilterCondition { Property = "name", Value = "different" });
            var point = new Point(new Coordinate(0, 0));
            var feature = new GeoFeature
            {
                Geometry = point,
                Properties = new Dictionary<string, object>
                {
                    { "name", "test" }
                }
            };
            
            // Act
            bool result = filter.Match(feature);
            
            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void Match_WithMultipleConditions_AllMustMatch()
        {
            // Arrange
            var filter = new GeoFilter();
            filter.Conditions.Add(new FilterCondition { Property = "name", Value = "test" });
            filter.Conditions.Add(new FilterCondition { Property = "type", Value = "point" });
            var point = new Point(new Coordinate(0, 0));
            var feature = new GeoFeature
            {
                Geometry = point,
                Properties = new Dictionary<string, object>
                {
                    { "name", "test" },
                    { "type", "point" }
                }
            };
            
            // Act
            bool result = filter.Match(feature);
            
            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void Match_WithMultipleConditions_OneDoesNotMatch()
        {
            // Arrange
            var filter = new GeoFilter();
            filter.Conditions.Add(new FilterCondition { Property = "name", Value = "test" });
            filter.Conditions.Add(new FilterCondition { Property = "type", Value = "line" });
            var point = new Point(new Coordinate(0, 0));
            var feature = new GeoFeature
            {
                Geometry = point,
                Properties = new Dictionary<string, object>
                {
                    { "name", "test" },
                    { "type", "point" }
                }
            };
            
            // Act
            bool result = filter.Match(feature);
            
            // Assert
            Assert.That(result, Is.False);
        }
    }
} 