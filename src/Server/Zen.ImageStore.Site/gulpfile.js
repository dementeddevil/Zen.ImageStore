/// <binding AfterBuild='default, postbuild:swagger' Clean='clean' />
var del = require('del'),
	gulp = require('gulp'),
	autoprefixer = require('gulp-autoprefixer'),
	concat = require('gulp-concat'),
	copy = require('gulp-copy'),
	cssmin = require('gulp-cssmin'),
	plumber = require('gulp-plumber'),
	postcss = require('gulp-postcss'),
	rename = require('gulp-rename'),
	replace = require('gulp-replace'),
	sass = require('gulp-sass'),
	sasslint = require('gulp-sass-lint'),
	sourcemaps = require('gulp-sourcemaps'),
	tslint = require('gulp-tslint'),
	tsc = require('gulp-typescript'),
	uglify = require('gulp-uglify'),
	watch = require('gulp-watch'),
	merge = require('merge2'),
	Builder = require('systemjs-builder'),
	Config = require('./gulpfile.config');

var config = new Config();
var tsProject = tsc.createProject(config.paths.tsRoot + 'tsconfig.json');

gulp.task('clean:js', function (cb) {
	var files = [
		config.paths.jsRoot + '**/*.js',
		config.paths.jsRoot + '**/*.js.map',
		'!' + config.paths.jsRoot + 'lib'
	];
	del(files, cb);
});

gulp.task('clean:css', function (cb) {
	var files = [
		config.paths.cssRoot + '**/*.css',
		config.paths.cssRoot + '**/*.css.map',
		'!' + config.paths.cssRoot + 'lib'
	];
	del(files, cb);
});

gulp.task('clean', ['clean:js', 'clean:css']);

gulp.task('copy:lib', function () {
	return merge([
		gulp.src(config.paths.nodeModulesSrc + 'systemjs/dist/*.js')
			.pipe(gulp.dest(config.paths.libDest + 'systemjs/')),
		gulp.src(config.paths.nodeModulesSrc + 'jquery/dist/*.js')
			.pipe(gulp.dest(config.paths.libDest + 'jquery/')),
		gulp.src(config.paths.nodeModulesSrc + 'tether/dist/js/*.js')
			.pipe(gulp.dest(config.paths.libDest + 'tether/')),
		gulp.src(config.paths.nodeModulesSrc + 'bootstrap/dist/js/bootstrap.js')
			.pipe(gulp.dest(config.paths.libDest + 'bootstrap/')),
		gulp.src(config.paths.nodeModulesSrc + 'angular2/bundles/*.js')
			.pipe(gulp.dest(config.paths.libDest + 'angular2/'))
	]);
});

gulp.task('lint:ts', function () {
	return gulp
		.src(config.paths.tsAppSelector)
		.pipe(tslint())
		.pipe(tslint.report('prose'));
});

gulp.task('build:js', function () {
	var sourceFiles = [
		config.paths.tsAppSelector,
        config.paths.tsTypings + 'index.d.ts'
	];

	var tsResult = gulp
        .src(sourceFiles)
		.pipe(plumber())
		.pipe(sourcemaps.init())
		.pipe(tsProject());

	return merge([
        tsResult.dts
            .pipe(gulp.dest(config.paths.tsAppDist)),
        tsResult.js
            .pipe(sourcemaps.write('.'))
            .pipe(gulp.dest(config.paths.tsAppDist))
	]);
});

gulp.task('build:sitecore', ['copy:lib'], function (cb) {
	var builder = new Builder(config.paths.webroot, config.paths.tsRoot + 'system-config.js');
	builder.buildStatic(
			config.paths.libDest + 'jquery/jquery.js + ' +
			config.paths.libDest + 'tether/tether.js + ' +
			config.paths.libDest + 'bootstrap/bootstrap.js',
			config.paths.jsRoot + 'sitecore.js')
		.then(function () {
			cb();
		})
		.catch(function (ex) {
			cb(ex);
		});
});

gulp.task('lint:css', function () {
	return gulp
		.src(config.paths.sassRootSelector)
		.pipe(sasslint())
		.pipe(sasslint.format())
		.pipe(sasslint.failOnError());
});

gulp.task('build:css:expanded', function () {
	var sourceFiles = [
        config.paths.sassRootSelector
	];
	var includePaths = [
		config.paths.nodeModulesSrc + 'bootstrap/scss/',
		config.paths.nodeModulesSrc + 'tether/src/css/'
	];
	return gulp
		.src(sourceFiles)
		.pipe(sourcemaps.init())
		.pipe(sass({
			outputStyle: 'expanded',
			includePaths: includePaths
		}).on('error', sass.logError))
		.pipe(autoprefixer('last 2 version'))
		.pipe(postcss([require('postcss-flexbugs-fixes')]))
		.pipe(sourcemaps.write())
		.pipe(gulp.dest(config.paths.cssRoot));
});

gulp.task('build:css:compressed', function () {
	var sourceFiles = [
        config.paths.sassRootSelector
	];
	var includePaths = [
		config.paths.nodeModulesSrc + 'bootstrap/scss/',
		config.paths.nodeModulesSrc + 'tether/src/css/'
	];
	return gulp
		.src(sourceFiles)
		.pipe(plumber())
		.pipe(sourcemaps.init())
		.pipe(sass({
			outputStyle: 'compressed',
			includePaths: includePaths
		}).on('error', sass.logError))
		.pipe(autoprefixer('last 2 version'))
		.pipe(postcss([require('postcss-flexbugs-fixes')]))
		.pipe(rename({
			suffix: '.min'
		}))
		.pipe(sourcemaps.write())
		.pipe(gulp.dest(config.paths.cssRoot));
});

gulp.task('build:css', ['build:css:expanded', 'build:css:compressed']);

gulp.task('build', ['build:sitecore', 'build:css', 'build:js']);

gulp.task('default', ['build']);

gulp.task('watch', ['build'], function () {
	gulp.watch(config.paths.tsAppSelector, ['build:js']);
	gulp.watch(config.paths.sassAllSelector, ['build:css']);
});

gulp.task('postbuild:swagger', function () {
    return gulp
        .src(config.paths.swaggerSourcePath)
        .pipe(gulp.dest(config.paths.swaggerDestinationPath));
});
