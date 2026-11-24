using System;
using System.Numerics;
using BlackThunder.UniEngine.SpatialSystem;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture, Category("SpatialSystem")]
    public class SpatialShapeTests
    {
        [Test]
        public void Basic2DShapesReadAndWriteLocalGeometry()
        {
            using var fixture = new SpatialFixture2D();
            var size = new SpatialFixture2D.Cell<Vector2>(Vector2.One);
            var offset = new SpatialFixture2D.Cell<Vector2>(Vector2.Zero);
            var radius = new SpatialFixture2D.Cell<float>(1f);
            var direction = new SpatialFixture2D.Cell<CapsuleDirection2D>(CapsuleDirection2D.Vertical);
            using var box = new BoxCollider2D(fixture.Space, fixture.Bindings(new()), size.Property, offset.Property);
            using var circle = new CircleCollider2D(
                fixture.Space,
                fixture.Bindings(new()),
                radius.Property,
                offset.Property);
            using var capsule = new CapsuleCollider2D(fixture.Space, fixture.Bindings(new()),
                size.Property, offset.Property, direction.Property);
            box.Size = new Vector2(2f, 3f);
            circle.Radius = 4f;
            capsule.Offset = new Vector2(5f, 6f);
            capsule.Direction = CapsuleDirection2D.Horizontal;
            Assert.That(size.Value, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(radius.Value, Is.EqualTo(4f));
            Assert.That(offset.Value, Is.EqualTo(new Vector2(5f, 6f)));
            Assert.That(direction.Value, Is.EqualTo(CapsuleDirection2D.Horizontal));
            radius.Value = 8f;
            Assert.That(circle.Radius, Is.EqualTo(8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => circle.Radius = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => capsule.Direction = (CapsuleDirection2D)9);
            Assert.Throws<ArgumentOutOfRangeException>(() => box.Size = -Vector2.One);
            capsule.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _ = capsule.Size);
        }

        [Test]
        public void Basic3DShapesReadAndWriteLocalGeometry()
        {
            using var fixture = new SpatialFixture3D();
            var size = new SpatialFixture3D.Cell<Vector3>(Vector3.One);
            var center = new SpatialFixture3D.Cell<Vector3>(Vector3.Zero);
            var radius = new SpatialFixture3D.Cell<float>(1f);
            var height = new SpatialFixture3D.Cell<float>(2f);
            var direction = new SpatialFixture3D.Cell<CapsuleDirection3D>(CapsuleDirection3D.Y);
            using var box = new BoxCollider3D(fixture.Space, fixture.Bindings(new()), size.Property, center.Property);
            using var sphere = new SphereCollider3D(
                fixture.Space,
                fixture.Bindings(new()),
                radius.Property,
                center.Property);
            using var capsule = new CapsuleCollider3D(fixture.Space, fixture.Bindings(new()),
                radius.Property, height.Property, center.Property, direction.Property);
            box.Size = new Vector3(2f, 3f, 4f);
            sphere.Radius = 4f;
            capsule.Center = new Vector3(5f, 6f, 7f);
            capsule.Height = 9f;
            capsule.Direction = CapsuleDirection3D.Z;
            Assert.That(size.Value, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(radius.Value, Is.EqualTo(4f));
            Assert.That(center.Value, Is.EqualTo(new Vector3(5f, 6f, 7f)));
            Assert.That(height.Value, Is.EqualTo(9f));
            Assert.That(direction.Value, Is.EqualTo(CapsuleDirection3D.Z));
            radius.Value = 8f;
            Assert.That(sphere.Radius, Is.EqualTo(8f));
            Assert.Throws<ArgumentOutOfRangeException>(() => sphere.Radius = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => capsule.Direction = (CapsuleDirection3D)9);
            Assert.Throws<ArgumentOutOfRangeException>(() => capsule.Height = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => box.Size = -Vector3.One);
            capsule.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _ = capsule.Height);
        }
    }
}
