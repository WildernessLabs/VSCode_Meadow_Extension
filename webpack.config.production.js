const { merge } = require('webpack-merge');
const commonConfig = require('./webpack.config.common');

const productionConfig = {
  mode: 'production',
  devtool: false, // No source maps for production
  // Other configurations specific to production mode...
};

module.exports = merge(commonConfig, productionConfig);