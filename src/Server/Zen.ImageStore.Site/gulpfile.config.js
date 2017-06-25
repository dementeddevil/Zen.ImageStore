'use strict';

var GulpConfig = (function () {
	function gulpConfig() {
		this.paths = {
			root: './',
			webroot: './wwwroot/'
		};

		// Setup root this.paths
		this.paths.nodeModulesSrc = this.paths.root + 'node_modules/';

		this.paths.tsRoot = this.paths.root + 'typescript/';
		this.paths.jsRoot = this.paths.webroot + 'js/';

		this.paths.tsAppRoot = this.paths.tsRoot + 'app/';
		this.paths.tsAppSelector = this.paths.tsAppRoot + '**/*.ts';

		this.paths.tsTypings = this.paths.tsRoot + 'typings/';
		this.paths.tsTypingsSelector = this.paths.tsTypings + '**/*.ts';

		this.paths.libDest = this.paths.jsRoot + 'lib/';
		this.paths.tsAppDist = this.paths.jsRoot + 'app/';

		this.paths.sassRoot = this.paths.root + 'stylesheets/';
		this.paths.sassAllSelector = this.paths.sassRoot + '**/*.scss';
		this.paths.sassRootSelector = this.paths.sassRoot + '**/[^_]*.scss';
		this.paths.cssRoot = this.paths.webroot + 'css/';

		this.paths.fontsDest = this.paths.webroot + 'fonts/';

	    this.paths.swaggerSourcePath = this.paths.root + 'bin/schema/Zen.ImageStore.Site.xml';
	    this.paths.swaggerDestinationPath = this.paths.webroot + 'schemas/api';
	}
	return gulpConfig;
})();
module.exports = GulpConfig;
