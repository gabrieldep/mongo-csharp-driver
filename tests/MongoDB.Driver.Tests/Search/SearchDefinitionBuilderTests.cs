﻿/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;
using MongoDB.Driver.Search;
using Xunit;

namespace MongoDB.Driver.Tests.Search
{
    public class SearchDefinitionBuilderTests
    {
        private static readonly GeoWithinBox<GeoJson2DGeographicCoordinates> __testBox =
            new GeoWithinBox<GeoJson2DGeographicCoordinates>(
                new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                    new GeoJson2DGeographicCoordinates(-161.323242, 22.065278)),
                new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                    new GeoJson2DGeographicCoordinates(-152.446289, 22.512557)));

        private static readonly GeoWithinCircle<GeoJson2DGeographicCoordinates> __testCircle =
            new GeoWithinCircle<GeoJson2DGeographicCoordinates>(
                new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                    new GeoJson2DGeographicCoordinates(-161.323242, 22.512557)),
                7.5);

        private static readonly GeoJsonPolygon<GeoJson2DGeographicCoordinates> __testPolygon =
            new GeoJsonPolygon<GeoJson2DGeographicCoordinates>(
                new GeoJsonPolygonCoordinates<GeoJson2DGeographicCoordinates>(
                    new GeoJsonLinearRingCoordinates<GeoJson2DGeographicCoordinates>(
                        new List<GeoJson2DGeographicCoordinates>()
                        {
                            new GeoJson2DGeographicCoordinates(-161.323242, 22.512557),
                            new GeoJson2DGeographicCoordinates(-152.446289, 22.065278),
                            new GeoJson2DGeographicCoordinates(-156.09375, 17.811456),
                            new GeoJson2DGeographicCoordinates(-161.323242, 22.512557)
                        })));

        [Fact]
        public void Autocomplete()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Autocomplete("x", "foo"),
                "{ autocomplete: { query: 'foo', path: 'x' } }");
            AssertRendered(
                subject.Autocomplete(new[] { "x", "y" }, "foo"),
                "{ autocomplete: { query: 'foo', path: ['x', 'y'] } }");
            AssertRendered(
                subject.Autocomplete("x", new[] { "foo", "bar" }),
                "{ autocomplete: { query: ['foo', 'bar'], path: 'x' } }");
            AssertRendered(
                subject.Autocomplete(new[] { "x", "y" }, new[] { "foo", "bar" }),
                "{ autocomplete: { query: ['foo', 'bar'], path: ['x', 'y'] } }");

            AssertRendered(
                subject.Autocomplete("x", "foo", SearchAutocompleteTokenOrder.Any),
                "{ autocomplete: { query: 'foo', path: 'x' } }");
            AssertRendered(
                subject.Autocomplete("x", "foo", SearchAutocompleteTokenOrder.Sequential),
                "{ autocomplete: { query: 'foo', path: 'x', tokenOrder: 'sequential' } }");

            AssertRendered(
                subject.Autocomplete("x", "foo", fuzzy: new SearchFuzzyOptions()),
                "{ autocomplete: { query: 'foo', path: 'x', fuzzy: {} } }");
            AssertRendered(
                subject.Autocomplete("x", "foo", fuzzy: new SearchFuzzyOptions()
                {
                    MaxEdits = 1,
                    PrefixLength = 5,
                    MaxExpansions = 25
                }),
                "{ autocomplete: { query: 'foo', path: 'x', fuzzy: { maxEdits: 1, prefixLength: 5, maxExpansions: 25 } } }");

            var scoreBuilder = new SearchScoreDefinitionBuilder<BsonDocument>();
            AssertRendered(
                subject.Autocomplete("x", "foo", score: scoreBuilder.Constant(1)),
                "{ autocomplete: { query: 'foo', path: 'x', score: { constant: { value: 1 } } } }");
        }

        [Fact]
        public void Autocomplete_typed()
        {
            var subject = CreateSubject<Person>();
            AssertRendered(
                subject.Autocomplete(x => x.FirstName, "foo"),
                "{ autocomplete: { query: 'foo', path: 'fn' } }");
            AssertRendered(
                subject.Autocomplete("FirstName", "foo"),
                "{ autocomplete: { query: 'foo', path: 'fn' } }");

            AssertRendered(
                subject.Autocomplete(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    "foo"),
                "{ autocomplete: { query: 'foo', path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Autocomplete(new[] { "FirstName", "LastName" }, "foo"),
                "{ autocomplete: { query: 'foo', path: ['fn', 'ln'] } }");

            AssertRendered(
                subject.Autocomplete(x => x.FirstName, new[] { "foo", "bar" }),
                "{ autocomplete: { query: ['foo', 'bar'], path: 'fn' } }");
            AssertRendered(
                subject.Autocomplete("FirstName", new[] { "foo", "bar" }),
                "{ autocomplete: { query: ['foo', 'bar'], path: 'fn' } }");

            AssertRendered(
                subject.Autocomplete(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    new[] { "foo", "bar" }),
                "{ autocomplete: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Autocomplete(new[] { "FirstName", "LastName" }, new[] { "foo", "bar" }),
                "{ autocomplete: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
        }

        [Fact]
        public void Compound()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered<BsonDocument>(
                subject.Compound()
                    .Must(
                        subject.Exists("x"),
                        subject.Exists("y"))
                    .MustNot(
                        subject.Exists("foo"),
                        subject.Exists("bar"))
                    .Must(
                        subject.Exists("z")),
                "{ compound: { must: [{ exists: { path: 'x' } }, { exists: { path: 'y' } }, { exists: { path: 'z' } }], mustNot: [{ exists: { path: 'foo' } }, { exists: { path: 'bar' } }] } }");
        }

        [Fact]
        public void Compound_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered<Person>(
                subject.Compound()
                    .Must(
                        subject.Exists(p => p.Age),
                        subject.Exists(p => p.FirstName))
                    .MustNot(
                        subject.Exists(p => p.Retired),
                        subject.Exists(p => p.Birthday))
                    .Must(
                        subject.Exists(p => p.LastName)),
                "{ compound: { must: [{ exists: { path: 'age' } }, { exists: { path: 'fn' } }, { exists: { path: 'ln' } }], mustNot: [{ exists: { path: 'ret' } }, { exists: { path: 'dob' } }] } }");
        }

        [Fact]
        public void Equals()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Equals("x", true),
                "{ equals: { path: 'x', value: true } }");
            AssertRendered(
                subject.Equals("x", ObjectId.Empty),
                "{ equals: { path: 'x', value: { $oid: '000000000000000000000000' } } }");

            var scoreBuilder = new SearchScoreDefinitionBuilder<BsonDocument>();
            AssertRendered(
                subject.Equals("x", true, scoreBuilder.Constant(1)),
                "{ equals: { path: 'x', value: true, score: { constant: { value: 1 } } } }");
        }

        [Fact]
        public void Equals_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Equals(x => x.Retired, true),
                "{ equals: { path: 'ret', value: true } }");
            AssertRendered(
                subject.Equals("Retired", true),
                "{ equals: { path: 'ret', value: true } }");

            AssertRendered(
                subject.Equals(x => x.Id, ObjectId.Empty),
                "{ equals: { path: '_id', value: { $oid: '000000000000000000000000' } } }");
        }

        [Fact]
        public void Exists()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Exists("x"),
                "{ exists: { path: 'x' } }");
        }

        [Fact]
        public void Exists_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Exists(x => x.FirstName),
                "{ exists: { path: 'fn' } }");
            AssertRendered(
                subject.Exists("FirstName"),
                "{ exists: { path: 'fn' } }");
        }

        [Fact]
        public void Facet()
        {
            var subject = CreateSubject<BsonDocument>();
            var facetBuilder = new SearchFacetBuilder<BsonDocument>();

            AssertRendered(
                subject.Facet(
                    subject.Phrase("x", "foo"),
                    facetBuilder.String("string", "y", 100)),
                "{ facet: { operator: { phrase: { query: 'foo', path: 'x' } }, facets: { string: { type: 'string', path: 'y', numBuckets: 100 } } } }");
        }

        [Fact]
        public void Facet_typed()
        {
            var subject = CreateSubject<Person>();
            var facetBuilder = new SearchFacetBuilder<Person>();

            AssertRendered(
                subject.Facet(
                    subject.Phrase(x => x.LastName, "foo"),
                    facetBuilder.String("string", x => x.FirstName, 100)),
                "{ facet: { operator: { phrase: { query: 'foo', path: 'ln' } }, facets: { string: { type: 'string', path: 'fn', numBuckets: 100 } } } }");
            AssertRendered(
                subject.Facet(
                    subject.Phrase("LastName", "foo"),
                    facetBuilder.String("string", "FirstName", 100)),
                "{ facet: { operator: { phrase: { query: 'foo', path: 'ln' } }, facets: { string: { type: 'string', path: 'fn', numBuckets: 100 } } } }");
        }

        [Fact]
        public void Filter()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered<BsonDocument>(
                subject.Compound().Filter(
                    subject.Exists("x"),
                    subject.Exists("y")),
                "{ compound: { filter: [{ exists: { path: 'x' } }, { exists: { path: 'y' } }] } }");
        }

        [Fact]
        public void Filter_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered<Person>(
                subject.Compound().Filter(
                    subject.Exists(p => p.Age),
                    subject.Exists(p => p.Birthday)),
                "{ compound: { filter: [{ exists: { path: 'age' } }, { exists: { path: 'dob' } }] } }");
        }

        [Fact]
        public void GeoShape()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.GeoShape(
                    "location",
                    GeoShapeRelation.Disjoint,
                    __testPolygon),
                "{ geoShape: { geometry: { type: 'Polygon', coordinates: [[[-161.323242, 22.512557], [-152.446289, 22.065278], [-156.09375, 17.811456], [-161.323242, 22.512557]]] }, path: 'location', relation: 'disjoint' } }");
        }

        [Fact]
        public void GeoShape_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.GeoShape(
                    x => x.Location,
                    GeoShapeRelation.Disjoint,
                    __testPolygon),
                "{ geoShape: { geometry: { type: 'Polygon', coordinates: [[[-161.323242, 22.512557], [-152.446289, 22.065278], [-156.09375, 17.811456], [-161.323242, 22.512557]]] }, path: 'location', relation: 'disjoint' } }");
            AssertRendered(
                subject.GeoShape(
                    "Location",
                    GeoShapeRelation.Disjoint,
                    __testPolygon),
                "{ geoShape: { geometry: { type: 'Polygon', coordinates: [[[-161.323242, 22.512557], [-152.446289, 22.065278], [-156.09375, 17.811456], [-161.323242, 22.512557]]] }, path: 'location', relation: 'disjoint' } }");
        }

        [Fact]
        public void GeoWithin()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.GeoWithin("location", __testPolygon),
                "{ geoWithin: { geometry: { type: 'Polygon', coordinates: [[[-161.323242, 22.512557], [-152.446289, 22.065278], [-156.09375, 17.811456], [-161.323242, 22.512557]]] }, path: 'location' } }");
            AssertRendered(
                subject.GeoWithin("location", __testBox),
                "{ geoWithin: { box: { bottomLeft: { type: 'Point', coordinates: [-161.323242, 22.065278] }, topRight: { type: 'Point', coordinates: [-152.446289, 22.512557] } }, path: 'location' } }");
            AssertRendered(
                subject.GeoWithin("location", __testCircle),
                "{ geoWithin: { circle: { center: { type: 'Point', coordinates: [-161.323242, 22.512557] }, radius: 7.5 }, path: 'location' } }");
        }

        [Fact]
        public void GeoWithin_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.GeoWithin(x => x.Location, __testPolygon),
                "{ geoWithin: { geometry: { type: 'Polygon', coordinates: [[[-161.323242, 22.512557], [-152.446289, 22.065278], [-156.09375, 17.811456], [-161.323242, 22.512557]]] }, path: 'location' } }");
            AssertRendered(
                subject.GeoWithin("Location", __testPolygon),
                "{ geoWithin: { geometry: { type: 'Polygon', coordinates: [[[-161.323242, 22.512557], [-152.446289, 22.065278], [-156.09375, 17.811456], [-161.323242, 22.512557]]] }, path: 'location' } }");

            AssertRendered(
                subject.GeoWithin(x => x.Location, __testBox),
                "{ geoWithin: { box: { bottomLeft: { type: 'Point', coordinates: [-161.323242, 22.065278] }, topRight: { type: 'Point', coordinates: [-152.446289, 22.512557] } }, path: 'location' } }");
            AssertRendered(
                subject.GeoWithin("Location", __testBox),
                "{ geoWithin: { box: { bottomLeft: { type: 'Point', coordinates: [-161.323242, 22.065278] }, topRight: { type: 'Point', coordinates: [-152.446289, 22.512557] } }, path: 'location' } }");

            AssertRendered(
                subject.GeoWithin(x => x.Location, __testCircle),
                "{ geoWithin: { circle: { center: { type: 'Point', coordinates: [-161.323242, 22.512557] }, radius: 7.5 }, path: 'location' } }");
            AssertRendered(
                subject.GeoWithin("Location", __testCircle),
                "{ geoWithin: { circle: { center: { type: 'Point', coordinates: [-161.323242, 22.512557] }, radius: 7.5 }, path: 'location' } }");
        }

        [Fact]
        public void MoreLikeThis()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.MoreLikeThis(
                    new BsonDocument("x", "foo"),
                    new BsonDocument("x", "bar")),
                "{ moreLikeThis: { like: [{ x: 'foo' }, { x: 'bar' }] } }");

            AssertRendered(
              subject.MoreLikeThis(
                  new SimplePerson { FirstName = "John", LastName = "Doe" },
                  new SimplePerson { FirstName = "Jane", LastName = "Doe" }),
             "{ moreLikeThis: { like: [{ fn: 'John', ln: 'Doe' }, { fn: 'Jane', ln: 'Doe' }] } }");
        }

        [Fact]
        public void MoreLikeThis_typed()
        {
            var subject = CreateSubject<SimplePerson>();

            AssertRendered(
                subject.MoreLikeThis(
                    new SimplestPerson { FirstName = "John" },
                    new SimplestPerson { FirstName = "Jane" }),
                "{ moreLikeThis: { like: [{ fn: 'John' }, { fn: 'Jane' }] } }");

            AssertRendered(
                subject.MoreLikeThis(
                    new SimplePerson { FirstName = "John", LastName = "Doe" },
                    new SimplePerson { FirstName = "Jane", LastName = "Doe" }),
                "{ moreLikeThis: { like: [{ fn: 'John', ln: 'Doe' }, { fn: 'Jane', ln: 'Doe' }] } }");

            AssertRendered(
               subject.MoreLikeThis(
                   new BsonDocument
                   {
                        { "fn", "John" },
                        { "ln", "Doe" },
                   },
                   new BsonDocument
                   {
                        { "fn", "Jane" },
                        { "ln", "Doe" },
                   }),
               "{ moreLikeThis: { like: [{ fn: 'John', ln: 'Doe' }, { fn: 'Jane', ln: 'Doe' }] } }");
        }

        [Fact]
        public void Must()
        {
            var subject = CreateSubject<Person>();

            AssertRendered<Person>(
                subject.Compound().Must(
                    subject.Exists(p => p.Age),
                    subject.Exists(p => p.Birthday)),
                "{ compound: { must: [{ exists: { path: 'age' } }, { exists: { path: 'dob' } }] } }");
        }

        [Fact]
        public void MustNot()
        {
            var subject = CreateSubject<Person>();

            AssertRendered<Person>(
                subject.Compound().MustNot(
                    subject.Exists(p => p.Age),
                    subject.Exists(p => p.Birthday)),
                "{ compound: { mustNot: [{ exists: { path: 'age' } }, { exists: { path: 'dob' } }] } }");
        }

        [Fact]
        public void Near()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Near("x", 5.0, 1.0),
                "{ near: { path: 'x', origin: 5.0, pivot: 1.0 } }");
            AssertRendered(
                subject.Near("x", 5, 1),
                "{ near: { path: 'x', origin: 5, pivot: 1 } }");
            AssertRendered(
                subject.Near("x", 5L, 1L),
                "{ near: { path: 'x', origin: { $numberLong: '5' }, pivot: { $numberLong: '1' } } }");
            AssertRendered(
                subject.Near("x", new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1000L),
                "{ near: { path: 'x', origin: { $date: '2000-01-01T00:00:00Z' }, pivot: { $numberLong: '1000' } } }");

            var scoreBuilder = new SearchScoreDefinitionBuilder<BsonDocument>();
            AssertRendered(
                subject.Near("x", 5.0, 1.0, scoreBuilder.Constant(1)),
                "{ near: { path: 'x', origin: 5, pivot: 1, score: { constant: { value: 1 } } } }");
        }

        [Fact]
        public void Near_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Near(x => x.Age, 35.0, 5.0),
                "{ near: { path: 'age', origin: 35.0, pivot: 5.0 } }");
            AssertRendered(
                subject.Near("Age", 35.0, 5.0),
                "{ near: { path: 'age', origin: 35.0, pivot: 5.0 } }");

            AssertRendered(
                subject.Near(x => x.Age, 35, 5),
                "{ near: { path: 'age', origin: 35, pivot: 5 } }");
            AssertRendered(
                subject.Near("Age", 35, 5),
                "{ near: { path: 'age', origin: 35, pivot: 5 } }");

            AssertRendered(
                subject.Near(x => x.Age, 35L, 5L),
                "{ near: { path: 'age', origin: { $numberLong: '35' }, pivot: { $numberLong: '5' } } }");
            AssertRendered(
                subject.Near("Age", 35L, 5L),
                "{ near: { path: 'age', origin: { $numberLong: '35' }, pivot: { $numberLong: '5' } } }");

            AssertRendered(
                subject.Near(x => x.Birthday, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1000L),
                "{ near: { path: 'dob', origin: { $date: '2000-01-01T00:00:00Z' }, pivot: { $numberLong: '1000' } } }");
            AssertRendered(
                subject.Near("Birthday", new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1000L),
                "{ near: { path: 'dob', origin: { $date: '2000-01-01T00:00:00Z' }, pivot: { $numberLong: '1000' } } }");
        }

        [Fact]
        public void Phrase()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Phrase("x", "foo"),
                "{ phrase: { query: 'foo', path: 'x' } }");
            AssertRendered(
                subject.Phrase(new[] { "x", "y" }, "foo"),
                "{ phrase: { query: 'foo', path: ['x', 'y'] } }");
            AssertRendered(
                subject.Phrase("x", new[] { "foo", "bar" }),
                "{ phrase: { query: ['foo', 'bar'], path: 'x' } }");
            AssertRendered(
                subject.Phrase(new[] { "x", "y" }, new[] { "foo", "bar" }),
                "{ phrase: { query: ['foo', 'bar'], path: ['x', 'y'] } }");

            AssertRendered(
                subject.Phrase("x", "foo", 5),
                "{ phrase: { query: 'foo', path: 'x', slop: 5 } }");

            var scoreBuilder = new SearchScoreDefinitionBuilder<BsonDocument>();
            AssertRendered(
                subject.Phrase("x", "foo", score: scoreBuilder.Constant(1)),
                "{ phrase: { query: 'foo', path: 'x', score: { constant: { value: 1 } } } }");
        }

        [Fact]
        public void Phrase_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Phrase(x => x.FirstName, "foo"),
                "{ phrase: { query: 'foo', path: 'fn' } }");
            AssertRendered(
                subject.Phrase("FirstName", "foo"),
                "{ phrase: { query: 'foo', path: 'fn' } }");

            AssertRendered(
                subject.Phrase(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    "foo"),
                "{ phrase: { query: 'foo', path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Phrase(new[] { "FirstName", "LastName" }, "foo"),
                "{ phrase: { query: 'foo', path: ['fn', 'ln'] } }");

            AssertRendered(
                subject.Phrase(x => x.FirstName, new[] { "foo", "bar" }),
                "{ phrase: { query: ['foo', 'bar'], path: 'fn' } }");
            AssertRendered(
                subject.Phrase("FirstName", new[] { "foo", "bar" }),
                "{ phrase: { query: ['foo', 'bar'], path: 'fn' } }");

            AssertRendered(
                subject.Phrase(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    new[] { "foo", "bar" }),
                "{ phrase: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Phrase(new[] { "FirstName", "LastName" }, new[] { "foo", "bar" }),
                "{ phrase: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
        }

        [Fact]
        public void QueryString()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.QueryString("x", "foo"),
                "{ queryString: { defaultPath: 'x', query: 'foo' } }");

            var scoreBuilder = new SearchScoreDefinitionBuilder<BsonDocument>();
            AssertRendered(
                subject.QueryString("x", "foo", scoreBuilder.Constant(1)),
                "{ queryString: { defaultPath: 'x', query: 'foo', score: { constant: { value: 1 } } } }");
        }

        [Fact]
        public void QueryString_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.QueryString(x => x.FirstName, "foo"),
                "{ queryString: { defaultPath: 'fn', query: 'foo' } }");
            AssertRendered(
                subject.QueryString("FirstName", "foo"),
                "{ queryString: { defaultPath: 'fn', query: 'foo' } }");
        }

        [Fact]
        public void RangeDateTime()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Range(
                    p => p.Birthday,
                    SearchRangeBuilder
                        .Gte(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                        .Lte(new DateTime(2009, 12, 31, 0, 0, 0, DateTimeKind.Utc))),
                "{ range: { path: 'dob', gte: { $date: '2000-01-01T00:00:00Z' }, lte: { $date: '2009-12-31T00:00:00Z' } } }");
        }

        [Fact]
        public void RangeDouble()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Range(p => p.Age, SearchRangeBuilder.Gt(1.5).Lt(2.5)),
                "{ range: { path: 'age', gt: 1.5, lt: 2.5 } }");
        }

        [Fact]
        public void RangeInt32()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Range("x", SearchRangeBuilder.Gt(1).Lt(10)),
                "{ range: { path: 'x', gt: 1, lt: 10 } }");
            AssertRendered(
                subject.Range("x", SearchRangeBuilder.Lt(10).Gt(1)),
                "{ range: { path: 'x', gt: 1, lt: 10 } }");
            AssertRendered(
                subject.Range("x", SearchRangeBuilder.Gte(1).Lte(10)),
                "{ range: { path: 'x', gte: 1, lte: 10 } }");
            AssertRendered(
                subject.Range("x", SearchRangeBuilder.Lte(10).Gte(1)),
                "{ range: { path: 'x', gte: 1, lte: 10 } }");
        }

        [Fact]
        public void RangeInt32_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Range(x => x.Age, SearchRangeBuilder.Gte(18).Lt(65)),
                "{ range: { path: 'age', gte: 18, lt: 65 } }");
            AssertRendered(
                subject.Range("Age", SearchRangeBuilder.Gte(18).Lt(65)),
                "{ range: { path: 'age', gte: 18, lt: 65 } }");
        }

        [Fact]
        public void Regex()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Regex("x", "foo"),
                "{ regex: { query: 'foo', path: 'x' } }");
            AssertRendered(
                subject.Regex(new[] { "x", "y" }, "foo"),
                "{ regex: { query: 'foo', path: ['x', 'y'] } }");
            AssertRendered(
                subject.Regex("x", new[] { "foo", "bar" }),
                "{ regex: { query: ['foo', 'bar'], path: 'x' } }");
            AssertRendered(
                subject.Regex(new[] { "x", "y" }, new[] { "foo", "bar" }),
                "{ regex: { query: ['foo', 'bar'], path: ['x', 'y'] } }");

            AssertRendered(
                subject.Regex("x", "foo", false),
                "{ regex: { query: 'foo', path: 'x' } }");
            AssertRendered(
                subject.Regex("x", "foo", true),
                "{ regex: { query: 'foo', path: 'x', allowAnalyzedField: true } }");

            var scoreBuilder = new SearchScoreDefinitionBuilder<BsonDocument>();
            AssertRendered(
                subject.Regex("x", "foo", score: scoreBuilder.Constant(1)),
                "{ regex: { query: 'foo', path: 'x', score: { constant: { value: 1 } } } }");
        }

        [Fact]
        public void Regex_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Regex(x => x.FirstName, "foo"),
                "{ regex: { query: 'foo', path: 'fn' } }");
            AssertRendered(
                subject.Regex("FirstName", "foo"),
                "{ regex: { query: 'foo', path: 'fn' } }");

            AssertRendered(
                subject.Regex(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    "foo"),
                "{ regex: { query: 'foo', path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Regex(new[] { "FirstName", "LastName" }, "foo"),
                "{ regex: { query: 'foo', path: ['fn', 'ln'] } }");

            AssertRendered(
                subject.Regex(x => x.FirstName, new[] { "foo", "bar" }),
                "{ regex: { query: ['foo', 'bar'], path: 'fn' } }");
            AssertRendered(
                subject.Regex("FirstName", new[] { "foo", "bar" }),
                "{ regex: { query: ['foo', 'bar'], path: 'fn' } }");

            AssertRendered(
                subject.Regex(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    new[] { "foo", "bar" }),
                "{ regex: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Regex(new[] { "FirstName", "LastName" }, new[] { "foo", "bar" }),
                "{ regex: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
        }

        [Fact]
        public void Should()
        {
            var subject = CreateSubject<Person>();

            AssertRendered<Person>(
                subject.Compound()
                    .Should(
                        subject.Exists(p => p.Age),
                        subject.Exists(p => p.Birthday))
                    .MinimumShouldMatch(2),
                "{ compound: { should: [{ exists: { path: 'age' } }, { exists: { path: 'dob' } }], minimumShouldMatch: 2 } }");
        }

        [Fact]
        public void Span()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Span(Builders<Person>.SearchSpan
                        .First(Builders<Person>.SearchSpan.Term(p => p.Age, "foo"), 5)),
                "{ span: { first: { operator: { term: { query: 'foo', path: 'age' } }, endPositionLte: 5 } } }");
        }

        [Fact]
        public void Text()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Text("x", "foo"),
                "{ text: { query: 'foo', path: 'x' } }");
            AssertRendered(
                subject.Text(new[] { "x", "y" }, "foo"),
                "{ text: { query: 'foo', path: ['x', 'y'] } }");
            AssertRendered(
                subject.Text("x", new[] { "foo", "bar" }),
                "{ text: { query: ['foo', 'bar'], path: 'x' } }");
            AssertRendered(
                subject.Text(new[] { "x", "y" }, new[] { "foo", "bar" }),
                "{ text: { query: ['foo', 'bar'], path: ['x', 'y'] } }");

            AssertRendered(
                subject.Text("x", "foo", new SearchFuzzyOptions()),
                "{ text: { query: 'foo', path: 'x', fuzzy: {} } }");
            AssertRendered(
                subject.Text("x", "foo", new SearchFuzzyOptions()
                {
                    MaxEdits = 1,
                    PrefixLength = 5,
                    MaxExpansions = 25
                }),
                "{ text: { query: 'foo', path: 'x', fuzzy: { maxEdits: 1, prefixLength: 5, maxExpansions: 25 } } }");

            var scoreBuilder = new SearchScoreDefinitionBuilder<BsonDocument>();
            AssertRendered(
                subject.Text("x", "foo", score: scoreBuilder.Constant(1)),
                "{ text: { query: 'foo', path: 'x', score: { constant: { value: 1 } } } }");
        }

        [Fact]
        public void Text_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Text(x => x.FirstName, "foo"),
                "{ text: { query: 'foo', path: 'fn' } }");
            AssertRendered(
                subject.Text("FirstName", "foo"),
                "{ text: { query: 'foo', path: 'fn' } }");

            AssertRendered(
                subject.Text(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    "foo"),
                "{ text: { query: 'foo', path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Text(new[] { "FirstName", "LastName" }, "foo"),
                "{ text: { query: 'foo', path: ['fn', 'ln'] } }");

            AssertRendered(
                subject.Text(x => x.FirstName, new[] { "foo", "bar" }),
                "{ text: { query: ['foo', 'bar'], path: 'fn' } }");
            AssertRendered(
                subject.Text("FirstName", new[] { "foo", "bar" }),
                "{ text: { query: ['foo', 'bar'], path: 'fn' } }");

            AssertRendered(
                subject.Text(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    new[] { "foo", "bar" }),
                "{ text: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Text(new[] { "FirstName", "LastName" }, new[] { "foo", "bar" }),
                "{ text: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
        }

        [Fact]
        public void Wildcard()
        {
            var subject = CreateSubject<BsonDocument>();

            AssertRendered(
                subject.Wildcard("x", "foo"),
                "{ wildcard: { query: 'foo', path: 'x' } }");
            AssertRendered(
                subject.Wildcard(new[] { "x", "y" }, "foo"),
                "{ wildcard: { query: 'foo', path: ['x', 'y'] } }");
            AssertRendered(
                subject.Wildcard("x", new[] { "foo", "bar" }),
                "{ wildcard: { query: ['foo', 'bar'], path: 'x' } }");
            AssertRendered(
                subject.Wildcard(new[] { "x", "y" }, new[] { "foo", "bar" }),
                "{ wildcard: { query: ['foo', 'bar'], path: ['x', 'y'] } }");

            AssertRendered(
                subject.Wildcard("x", "foo", false),
                "{ wildcard: { query: 'foo', path: 'x' } }");
            AssertRendered(
                subject.Wildcard("x", "foo", true),
                "{ wildcard: { query: 'foo', path: 'x', allowAnalyzedField: true } }");

            var scoreBuilder = new SearchScoreDefinitionBuilder<BsonDocument>();
            AssertRendered(
                subject.Wildcard("x", "foo", score: scoreBuilder.Constant(1)),
                "{ wildcard: { query: 'foo', path: 'x', score: { constant: { value: 1 } } } }");
        }

        [Fact]
        public void Wildcard_typed()
        {
            var subject = CreateSubject<Person>();

            AssertRendered(
                subject.Wildcard(x => x.FirstName, "foo"),
                "{ wildcard: { query: 'foo', path: 'fn' } }");
            AssertRendered(
                subject.Wildcard("FirstName", "foo"),
                "{ wildcard: { query: 'foo', path: 'fn' } }");

            AssertRendered(
                subject.Wildcard(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    "foo"),
                "{ wildcard: { query: 'foo', path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Wildcard(new[] { "FirstName", "LastName" }, "foo"),
                "{ wildcard: { query: 'foo', path: ['fn', 'ln'] } }");

            AssertRendered(
                subject.Wildcard(x => x.FirstName, new[] { "foo", "bar" }),
                "{ wildcard: { query: ['foo', 'bar'], path: 'fn' } }");
            AssertRendered(
                subject.Wildcard("FirstName", new[] { "foo", "bar" }),
                "{ wildcard: { query: ['foo', 'bar'], path: 'fn' } }");

            AssertRendered(
                subject.Wildcard(
                    new FieldDefinition<Person>[]
                    {
                        new ExpressionFieldDefinition<Person, string>(x => x.FirstName),
                        new ExpressionFieldDefinition<Person, string>(x => x.LastName)
                    },
                    new[] { "foo", "bar" }),
                "{ wildcard: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
            AssertRendered(
                subject.Wildcard(new[] { "FirstName", "LastName" }, new[] { "foo", "bar" }),
                "{ wildcard: { query: ['foo', 'bar'], path: ['fn', 'ln'] } }");
        }

        private void AssertRendered<TDocument>(SearchDefinition<TDocument> query, string expected) =>
            AssertRendered(query, BsonDocument.Parse(expected));

        private void AssertRendered<TDocument>(SearchDefinition<TDocument> query, BsonDocument expected)
        {
            var documentSerializer = BsonSerializer.SerializerRegistry.GetSerializer<TDocument>();
            var renderedQuery = query.Render(documentSerializer, BsonSerializer.SerializerRegistry);

            renderedQuery.Should().BeEquivalentTo(expected);
        }

        private SearchDefinitionBuilder<TDocument> CreateSubject<TDocument>() => new SearchDefinitionBuilder<TDocument>();

        private class Person : SimplePerson
        {
            [BsonElement("age")]
            public int Age { get; set; }

            [BsonElement("dob")]
            public DateTime Birthday { get; set; }

            [BsonId]
            public ObjectId Id { get; set; }
            [BsonElement("location")]
            public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; }

            [BsonElement("ret")]
            public bool Retired { get; set; }
        }

        private class SimplePerson
        {
            [BsonElement("fn")]
            public string FirstName { get; set; }

            [BsonElement("ln")]
            public string LastName { get; set; }
        }

        private class SimplestPerson
        {
            [BsonElement("fn")]
            public string FirstName { get; set; }
        }
    }
}
