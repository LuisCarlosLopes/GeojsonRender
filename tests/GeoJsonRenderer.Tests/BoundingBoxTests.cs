using System;
using NUnit.Framework;
using GeoJsonRenderer.Domain.Models;

namespace GeoJsonRenderer.Tests
{
    public class BoundingBoxTests
    {
        [Test]
        public void ConstructWithValues_ValidBoundingBox()
        {
            var bbox = new BoundingBox(-10, -10, 10, 10);

            Assert.That(bbox.MinX, Is.EqualTo(-10));
            Assert.That(bbox.MinY, Is.EqualTo(-10));
            Assert.That(bbox.MaxX, Is.EqualTo(10));
            Assert.That(bbox.MaxY, Is.EqualTo(10));
        }

        [Test]
        public void IsValid_ValidBoundingBox_ReturnsTrue()
        {
            var bbox = new BoundingBox(-10, -10, 10, 10);

            Assert.That(bbox.IsValid(), Is.True);
        }

        [Test]
        public void IsValid_InvalidBoundingBox_ReturnsFalse()
        {
            var bbox = new BoundingBox(10, 10, -10, -10);

            Assert.That(bbox.IsValid(), Is.False);
        }

        [Test]
        public void Expand_NullBoundingBox_DoesNotThrow()
        {
            var bbox = new BoundingBox(-10, -10, 10, 10);
            BoundingBox other = null;
            // Não deve lançar exceção
            Assert.DoesNotThrow(() => bbox.Expand(other));
        }

        [Test]
        public void Expand_LargerBoundingBox_ExpandsToIncludeIt()
        {
            var bbox = new BoundingBox(-10, -10, 10, 10);
            var other = new BoundingBox(-20, -20, 20, 20);

            bbox.Expand(other);

            Assert.That(bbox.MinX, Is.EqualTo(-20));
            Assert.That(bbox.MinY, Is.EqualTo(-20));
            Assert.That(bbox.MaxX, Is.EqualTo(20));
            Assert.That(bbox.MaxY, Is.EqualTo(20));
        }

        [Test]
        public void Expand_SmallerBoundingBox_RemainsUnchanged()
        {
            var bbox = new BoundingBox(-20, -20, 20, 20);
            var other = new BoundingBox(-10, -10, 10, 10);

            bbox.Expand(other);

            Assert.That(bbox.MinX, Is.EqualTo(-20));
            Assert.That(bbox.MinY, Is.EqualTo(-20));
            Assert.That(bbox.MaxX, Is.EqualTo(20));
            Assert.That(bbox.MaxY, Is.EqualTo(20));
        }

        [Test]
        public void Expand_PartiallyOverlappingBoundingBox_ExpandsAppropriately()
        {
            var bbox = new BoundingBox(-10, -10, 10, 10);
            var other = new BoundingBox(0, 0, 20, 20);

            bbox.Expand(other);

            Assert.That(bbox.MinX, Is.EqualTo(-10));
            Assert.That(bbox.MinY, Is.EqualTo(-10));
            Assert.That(bbox.MaxX, Is.EqualTo(20));
            Assert.That(bbox.MaxY, Is.EqualTo(20));
        }

        [Test]
        public void GetCenter_ReturnsCentralPoint()
        {
            var bbox = new BoundingBox(-10, -10, 10, 10);
            var center = bbox.GetCenter();

            Assert.That(center.X, Is.EqualTo(0));
            Assert.That(center.Y, Is.EqualTo(0));
        }

        [Test]
        public void GetCenter_UnevenBoundingBox_ReturnsCentralPoint()
        {
            var bbox = new BoundingBox(-20, -10, 20, 30);
            var center = bbox.GetCenter();

            Assert.That(center.X, Is.EqualTo(0));
            Assert.That(center.Y, Is.EqualTo(10));
        }
    }
}