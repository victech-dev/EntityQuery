# EntityQuery

## 개요

Dapper를 사용함에 있어서 table을 entity 개념으로 다루기 위한 extensions 모음 라이브러리

기존에 mz project 에서 컬럼 명을 Dto 클래스에 hardcoding 하거나 하는 등의 low level, 반복적인 작업을 많이 했는데,

이를 entity 클래스 선언으로 어느 정도 해결할 수 있도록 도움을 주는 기능들의 모음이다.

## 기본 Base code

원래 [Dapper.SimpleCRUD](https://github.com/ericdc1/Dapper.SimpleCRUD) 를 도입하려 했는데,

이 library가 정확히 entity로만 사용해야 하는 것에 초점을 맞춘 라이브러리여서 mz의 복잡한 쿼리 등에 대응이 잘 안되는 상황.

그래서 이 라이브러리를 기반으로 코드를 재구성해서 우리 실정에 맞춰 사용할 수 있게 코드를 재작성 하였다.

만약 원래 컨셉에 대한 기능을 보고 싶으면 [Dapper.SimpleCRUD](https://github.com/ericdc1/Dapper.SimpleCRUD) 를 참고해도 좋다.

(첨언하자면, 원래 코드가 좀 많이 정리가 안되있는 상황이었음)

코드를 재작성하면서, 다음 기능은 고려하지 않았다.

- interface entity에 대한 지원
- MySQL이 아닌 다른 db에 대한 dialect 지원
- guid key 에 대한 지원

코드를 재작성하면서, 추가로 고려한 기능은 다음과 같다.

- record 에 대한 지원
- query 생성과 실행하는 기능의 분리
- cache 기능의 강화
- upsert 지원

## 세부 기능들

### EqBuilder

정의된 entity class( or record)에 대해서 CRUD에 해당하는 select, insert, update, delete에 대한 쿼리를 쉽게 작성할 수 있게 하는 빌더 패턴의 extentions methods들

이 빌더는 cache를 위해서 순수하게 deterministic 한 쿼리를 생성하는 것이 컨셉이다.

즉, $"WHERE age={age}" 같은 hard coding 된 파라미터 추가 등을 배재한다.

위와 같은 기능은 실제 라이브러리를 사용하는 측에서 cache를 끄고 사용하던가 해야 한다.

기본 예제를 보면 이해가 쉽다.

```c#

// entity 클래스
[Table("Users")]
public record UserRecord(int Id, string Name, int Age, int? ScheduledDayOff);

// id로 select 하는 쿼리
var selectQuery = EqBuilder<UserRecord>.Create().Select().Build();

// 특정 where 조건으로 update
var updateQuery = EqBuilder<UserRecord>
    .Create(cacheKey)
    .UpdateSet()
    .Append(" ")
    .Append("age = @Age")
    .Where("name = @Name")
    .Build();

```

### EntityQueryExecuteExtensions

일반적으로 자주 사용하게 되는 crud 패턴들에 대해서 query build 후

DbConnection을 통해 실제 실행까지 하는 extensions methods들.

내부적으로 QueryBuild 를 사용한다.

기본 예제를 보면 이해가 쉽다.

```c#
[Table("Users")]
public record UserRecord(int Id, string Name, int Age, int? ScheduledDayOff);
var entity = new UserRecord(22, "bob", 30, null);

// entity insert
var affectedRows = await con.InsertAsync(entity);

// select with where
var entities = await con.SelectAsync("age = @Age", new { Age = 30 });

// entity delete
var affectedRows = await con.DeleteAsync(entity);

```

### EntityQueryExtensions.cs

각 entity 타입 별로 필요한 추가적인 기능들의 모음

예를 들어 select의 컬럼이나, update 의 set 구문등을 reflection을 통해 생성하고 이를 캐싱하는 기능 등을 포함한다.

### 사용하는 attributes 들

#### System.ComponentModel.DataAnnotations.Schema.TableAttribute

테이블의 이름이 entity 이름과 다를 경우 지정하는 용도

#### System.ComponentModel.DataAnnotations.Schema.ColumnAttribute

컬럼 이름이 entity의 이름과 다른 경우 이를 지정한다.

참고로 snake_case에 대한 지원은 Dapper.DefaultTypeMap.MatchNamesWithUnderscores 의 값을 따라간다.

#### System.ComponentModel.ReadOnlyAttribute

읽기 전용 컬럼인 경우 ex. created_at

#### System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute

엔티티에 정의는 되어있지만 table과 관련이 없는 property인 경우에 사용

#### System.ComponentModel.DataAnnotations.KeyAttribute

PK 컬럼을 지정하는 용도. 기본적으로 이름이 Id인 컬럼이 있다면 이를 key로 사용한다.

#### System.ComponentModel.DataAnnotations.EditableAttribute

[ Editable(AllowEdit=false) ] 로 지정되면 이 property는 mapping 에서 제외된다.

## 테스트 프로젝트

원래 SimpleCRUD 라이브러리가 가지고 있던 옛날 방식의 테스트 기능들을 xunit을 사용해서 많이 사용하는 형태로 재작성하였다.

특정 기능의 대한 샘플 코드나 이해가 필요한 경우, unit test를 훓어보면 좋다.
