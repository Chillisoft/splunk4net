var gulp = require('gulp');
var msbuild = require('gulp-msbuild');
gulp.task('build', ['nuget-restore'], function() {
    return gulp.src('**/*.sln')
            .pipe(msbuild({
                toolsVersion: 4.0,  // use 12.0 rather? 4.0 is supported by Mono...
                targets: ['Clean', 'Build'],
                configuration: 'Debug',
                stdout: true,
                verbosity: 'minimal',
                errorOnFail: true,
                architecture: 'x86' // sqlite :/
            }));
});


